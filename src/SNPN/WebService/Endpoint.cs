using Autofac;
using Microsoft.Extensions.Caching.Memory;
using Nancy;
using Nancy.ModelBinding;
using Serilog;
using SNPN.Common;
using SNPN.Data;
using SNPN.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNPN.WebService
{
	public sealed class Endpoint : NancyModule
	{
		private readonly MemoryCache cache;
		private readonly IUserRepo userRepo;
		private readonly ILogger logger;
		private readonly INetworkService networkService;

		public Endpoint()
		{
			Post("/register", this.RegisterDevice);
			Post("/deregister", this.DeregisterDevice);
			Post("/replyToNotification", this.ReplyToNotification);
			Get("/test", x => new { status = "ok" });
			Get("tileContent", this.GetTileContent);
			Post("/user", this.PostUser);
			Get("/user", this.GetUser);

			#region Deprecated Routes 
			//Remove these in a future update, once the app has been updated to not call them.
			//For now, just return default results.
			Post("/resetcount", x => new { status = "success" });
			Post("/removeNotification", x => new { status = "success" });
			Get("/openReplyNotifications", x => new { data = new List<int>() });
			#endregion

			this.cache = AppModuleBuilder.Container.Resolve<MemoryCache>();
			this.userRepo = AppModuleBuilder.Container.Resolve<IUserRepo>();
			this.logger = AppModuleBuilder.Container.Resolve<ILogger>();
			this.networkService = AppModuleBuilder.Container.Resolve<INetworkService>();
		}

		#region Event Bind Classes
		// ReSharper disable once ClassNeverInstantiated.Local
		private class RegisterArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string UserName { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string DeviceId { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string ChannelUri { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		private class PostUserArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string UserName { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public long NotifyOnUserName { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		private class GetUserArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string UserName { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		private class DeregisterArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string DeviceId { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		private class ReplyToNotificationArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string UserName { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string Password { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string ParentId { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string Text { get; set; }
		}
		#endregion

		private async Task<dynamic> PostUser(dynamic arg)
		{
			var e = this.Bind<PostUserArgs>();
			this.logger.Information("Updating user {userName}.", e.UserName);
			var user = await this.userRepo.FindUser(e.UserName);
			if (user == null)
			{
				await this.userRepo.AddUser(new NotificationUser
				{
					UserName = e.UserName,
					DateAdded = DateTime.UtcNow,
					NotifyOnUserName = e.NotifyOnUserName
				});
			}
			else
			{
				user.NotifyOnUserName = e.NotifyOnUserName;
				await this.userRepo.UpdateUser(user);
			}

			return new { status = "success" };
		}

		private async Task<dynamic> GetUser(dynamic arg)
		{
			var e = this.Bind<GetUserArgs>();
			this.logger.Information("Getting user {userName}.", e.UserName);
			var user = await this.userRepo.FindUser(e.UserName);
			if (user != null)
			{
				return new { user.UserName, NotifyOnUserName = user.NotifyOnUserName == 0 };
			}
			throw new Exception("User not found.");
		}

		private async Task<dynamic> GetTileContent(dynamic arg)
		{
			var tileContent = this.cache.Get("tileContent") as string;
			if (string.IsNullOrWhiteSpace(tileContent))
			{
				this.logger.Information("Retrieving tile content.");

				var xDoc = await this.networkService.GetTileContent();
				
				var items = xDoc.Descendants("item");
				var itemsObj = items.Select(i => new
				{
					Title = i.Element("title").Value,
					PublishDate = DateTime.Parse(i.Element("pubDate").Value.Replace("PDT", "").Replace("PST", "").Trim()),
					Author = i.Element("author").Value
				}).OrderByDescending(i => i.PublishDate).Take(3).ToList();

				var item = itemsObj.FirstOrDefault();

				if (item == null) return string.Empty;

				var visualElement = new XElement("visual", new XAttribute("version", "2"));
				var tileElement = new XElement("tile", visualElement);

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileWide310x150Text09"), new XAttribute("fallback", "TileWideText09"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title)));

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileSquare150x150Text02"), new XAttribute("fallback", "TileSquareText02"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title)));

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileSquare310x310TextList03"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title),
					new XElement("text", new XAttribute("id", "3"), $"{ itemsObj.ElementAt(1).Author} posted"),
					new XElement("text", new XAttribute("id", "4"), itemsObj.ElementAt(1).Title),
					new XElement("text", new XAttribute("id", "5"), $"{itemsObj.ElementAt(2).Author} posted"),
					new XElement("text", new XAttribute("id", "6"), itemsObj.ElementAt(2).Title)));
				var doc = new XDocument(tileElement);
				tileContent = doc.ToString(SaveOptions.DisableFormatting);
				this.cache.Set("tileContent", tileContent, DateTimeOffset.UtcNow.AddMinutes(5));
			}
			else
			{
				this.logger.Information("Retrieved cached tile content.");
			}
			return tileContent;
		}

		private async Task<dynamic> ReplyToNotification(dynamic arg)
		{
			this.logger.Information("Replying to notification.");
			var e = this.Bind<ReplyToNotificationArgs>();

			var success = await this.networkService.ReplyToNotification(e.Text, e.ParentId, e.UserName, e.Password);
			return new { status = success ? "success" : "error" };
		}

		private async Task<dynamic> DeregisterDevice(dynamic arg)
		{
			this.logger.Information("Deregister device.");
			var e = this.Bind<DeregisterArgs>();

			await this.userRepo.DeleteDevice(e.DeviceId);
			return new { status = "success" };
		}

		private async Task<dynamic> RegisterDevice(dynamic arg)
		{
			this.logger.Information("Register device.");
			var e = this.Bind<RegisterArgs>();

			var user = await this.userRepo.FindUser(e.UserName) ?? await this.userRepo.AddUser(new NotificationUser
			{
				UserName = e.UserName,
				DateAdded = DateTime.UtcNow,
				NotifyOnUserName = 1
			});

			await this.userRepo.AddOrUpdateDevice(user, new DeviceInfo { DeviceId = e.DeviceId, NotificationUri = e.ChannelUri });
			return new { status = "success" };
		}
	}
}
