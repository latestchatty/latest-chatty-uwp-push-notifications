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
		private readonly IUserRepo _userRepo;
		private readonly ILogger _logger;
		private readonly INetworkService _networkService;
		private readonly TileContentRepo _tileContentRepo;

		public Endpoint()
		{
			Post("/register", RegisterDevice);
			Post("/deregister", DeregisterDevice);
			Post("/replyToNotification", ReplyToNotification);
			Get("/test", x => new { status = "ok" });
			Get("tileContent", GetTileContent);
			Post("/user", PostUser);
			Get("/user", GetUser);

			#region Deprecated Routes 
			//Remove these in a future update, once the app has been updated to not call them.
			//For now, just return default results.
			Post("/resetcount", x => new { status = "success" });
			Post("/removeNotification", x => new { status = "success" });
			Get("/openReplyNotifications", x => new { data = new List<int>() });
			#endregion
			
			_userRepo = AppModuleBuilder.Container.Resolve<IUserRepo>();
			_logger = AppModuleBuilder.Container.Resolve<ILogger>();
			_networkService = AppModuleBuilder.Container.Resolve<INetworkService>();
			_tileContentRepo = AppModuleBuilder.Container.Resolve<TileContentRepo>();
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
			_logger.Information("Updating user {userName}.", e.UserName);
			var user = await _userRepo.FindUser(e.UserName);
			if (user == null)
			{
				await _userRepo.AddUser(new NotificationUser
				{
					UserName = e.UserName,
					DateAdded = DateTime.UtcNow,
					NotifyOnUserName = e.NotifyOnUserName
				});
			}
			else
			{
				user.NotifyOnUserName = e.NotifyOnUserName;
				await _userRepo.UpdateUser(user);
			}

			return new { status = "success" };
		}

		private async Task<dynamic> GetUser(dynamic arg)
		{
			var e = this.Bind<GetUserArgs>();
			_logger.Information("Getting user {userName}.", e.UserName);
			var user = await _userRepo.FindUser(e.UserName);
			if (user != null)
			{
				return new { user.UserName, NotifyOnUserName = user.NotifyOnUserName == 0 };
			}
			throw new Exception("User not found.");
		}

		private async Task<dynamic> GetTileContent(dynamic arg)
		{
			return await _tileContentRepo.GetTileContent();
		}

		private async Task<dynamic> ReplyToNotification(dynamic arg)
		{
			_logger.Information("Replying to notification.");
			var e = this.Bind<ReplyToNotificationArgs>();

			var success = await _networkService.ReplyToNotification(e.Text, e.ParentId, e.UserName, e.Password);
			return new { status = success ? "success" : "error" };
		}

		private async Task<dynamic> DeregisterDevice(dynamic arg)
		{
			_logger.Information("Deregister device.");
			var e = this.Bind<DeregisterArgs>();

			await _userRepo.DeleteDevice(e.DeviceId);
			return new { status = "success" };
		}

		private async Task<dynamic> RegisterDevice(dynamic arg)
		{
			_logger.Information("Register device.");
			var e = this.Bind<RegisterArgs>();

			var user = await _userRepo.FindUser(e.UserName) ?? await _userRepo.AddUser(new NotificationUser
			{
				UserName = e.UserName,
				DateAdded = DateTime.UtcNow,
				NotifyOnUserName = 1
			});

			await _userRepo.AddOrUpdateDevice(user, new DeviceInfo { DeviceId = e.DeviceId, NotificationUri = e.ChannelUri });
			return new { status = "success" };
		}
	}
}
