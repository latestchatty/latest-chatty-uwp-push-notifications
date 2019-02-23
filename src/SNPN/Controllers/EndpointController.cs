using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SNPN.Common;
using SNPN.Data;
using SNPN.Model;

namespace SNPN.Controllers
{
	[Route("/")]
	[ApiController]
	public sealed class EndpointController : Controller
	{
		private readonly IUserRepo _userRepo;
		private readonly ILogger _logger;
		private readonly INetworkService _networkService;
		private readonly TileContentRepo _tileContentRepo;

		public EndpointController(IUserRepo userRepo, ILogger logger, INetworkService networkService, TileContentRepo tileContentRepo)
		{
			_userRepo = userRepo;
			_logger = logger;
			_networkService = networkService;
			_tileContentRepo = tileContentRepo;
		}

		#region Event Bind Classes
		// ReSharper disable once ClassNeverInstantiated.Local
		public class RegisterArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string UserName { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string DeviceId { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string ChannelUri { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		public class PostUserArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string UserName { get; set; }
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public long NotifyOnUserName { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		public class GetUserArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string UserName { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		public class DeregisterArgs
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public string DeviceId { get; set; }
		}

		// ReSharper disable once ClassNeverInstantiated.Local
		public class ReplyToNotificationArgs
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

		#region Deprecated Routes 
		//Remove these in a future update, once the app has been updated to not call them.
		//For now, just return default results.
		[HttpPost("resetcount")]
		public dynamic ResetCount() => new { status = "success" };
		[HttpPost("removeNotification")]
		public dynamic RemoveNotification() => new { status = "success" };
		[HttpPost("openReplyNotifications")]
		public dynamic OpenReplyNotifications() => new { data = new List<int>() };

		#endregion

		[HttpGet("test")]
		public dynamic GetTest() => new { status = "ok", version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() };
			
		[HttpPost("user")]
		public async Task<IActionResult> PostUser([FromBody] PostUserArgs e)
		{
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

			return Json(new { status = "success" });
		}

		[HttpGet("user")]
		public async Task<IActionResult> GetUser(string userName)
		{
			_logger.Information("Getting user {userName}.", userName);
			var user = await _userRepo.FindUser(userName);
			if (user != null)
			{
				return Json(new { user.UserName, NotifyOnUserName = user.NotifyOnUserName == 0 });
			}
			throw new Exception("User not found.");
		}

		[HttpGet("tileContent")]
		public async Task<IActionResult> GetTileContent(dynamic arg)
		{
			return Json(await _tileContentRepo.GetTileContent());
		}

		[HttpPost("replyToNotification")]
		public async Task<IActionResult> ReplyToNotification([FromBody] ReplyToNotificationArgs e)
		{
			_logger.Information("Replying to notification.");

			var success = await _networkService.ReplyToNotification(e.Text, e.ParentId, e.UserName, e.Password);
			return Json(new { status = success ? "success" : "error" });
		}

		[HttpPost("deregister")]
		public async Task<IActionResult> DeregisterDevice([FromBody] DeregisterArgs e)
		{
			_logger.Information("Deregister device.");

			await _userRepo.DeleteDevice(e.DeviceId);
			return Json(new { status = "success" });
		}

		[HttpPost("register")]
		public async Task<IActionResult> RegisterDevice([FromBody] RegisterArgs e)
		{
			_logger.Information("Register device.");

			var user = await _userRepo.FindUser(e.UserName) ?? await _userRepo.AddUser(new NotificationUser
			{
				UserName = e.UserName,
				DateAdded = DateTime.UtcNow,
				NotifyOnUserName = 1
			});

			await _userRepo.AddOrUpdateDevice(user, new DeviceInfo { DeviceId = e.DeviceId, NotificationUri = e.ChannelUri });
			return Json(new { status = "success" });
		}
	}
}
