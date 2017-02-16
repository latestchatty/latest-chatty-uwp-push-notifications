using Autofac;
using Nancy;
using Nancy.ModelBinding;
using Serilog;
using SNPN.Common;
using SNPN.Data;
using SNPN.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SNPN.WebService
{
	public sealed class Endpoint : NancyModule
	{
		private readonly IUserRepo userRepo;
		private readonly ILogger logger;
		private readonly INetworkService networkService;
		private readonly TileContentRepo tileContentRepo;

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
			
			this.userRepo = AppModuleBuilder.Container.Resolve<IUserRepo>();
			this.logger = AppModuleBuilder.Container.Resolve<ILogger>();
			this.networkService = AppModuleBuilder.Container.Resolve<INetworkService>();
			this.tileContentRepo = AppModuleBuilder.Container.Resolve<TileContentRepo>();
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
			return await this.tileContentRepo.GetTileContent();
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
