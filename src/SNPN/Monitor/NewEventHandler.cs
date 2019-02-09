using Serilog;
using SNPN.Common;
using SNPN.Data;
using SNPN.Model;
using System;
using System.Threading.Tasks;

namespace SNPN.Monitor
{
	public class NewEventHandler
	{
		private readonly ILogger _logger;
		private readonly INotificationService _notificationService;
		private readonly IUserRepo _userRepo;
		private const int Ttl = 172800; // 48 hours

		public NewEventHandler(INotificationService notificationService, IUserRepo userRepo, ILogger logger)
		{
			_notificationService = notificationService;
			_userRepo = userRepo;
			_logger = logger;
		}

		public async Task ProcessEvent(NewPostEvent e)
		{
			var postBody = HtmlRemoval.StripTagsRegexCompiled(System.Net.WebUtility.HtmlDecode(e.Post.Body).Replace("<br />", " ").Replace(char.ConvertFromUtf32(8232), " "));
			//Don't notify if self-reply.
			if (!e.ParentAuthor.Equals(e.Post.Author, StringComparison.OrdinalIgnoreCase))
			{
				var usr = await _userRepo.FindUser(e.ParentAuthor);
				if (usr != null)
				{
					NotifyUser(usr, e.Post.Id, $"Reply from {e.Post.Author}", postBody);
				}
				else
				{
					_logger.Verbose("No alert on reply to {parentAuthor}", e.ParentAuthor);
				}
			}
			else
			{
				_logger.Verbose("No alert on self-reply to {parentAuthor}", e.ParentAuthor);
			}

			var users = await _userRepo.GetAllUserNamesForNotification();
			foreach (var user in users)
			{
				//Pad with spaces so we don't match a partial username.
				if ((" " + postBody.ToLower() + " ").Contains(" " + user.ToLower() + " "))
				{
					var u1 = await _userRepo.FindUser(user);
					if (u1 != null)
					{
						_logger.Information("Notifying {user} of mention by {latestReplyAuthor}",
							user, e.Post.Author);
						NotifyUser(u1, e.Post.Id, $"Mentioned by {e.Post.Author}", postBody);
					}
				}
			}
		}

		private async void NotifyUser(NotificationUser user, int latestPostId, string title, string message)
		{
			var deviceInfos = await _userRepo.GetUserDeviceInfos(user);

			foreach (var info in deviceInfos)
			{
				var toastDoc = NotificationBuilder.BuildReplyDoc(latestPostId, title, message);
				_notificationService.QueueNotificationData(
					NotificationType.Toast, 
					info.NotificationUri, 
					toastDoc, 
					NotificationGroups.ReplyToUser, 
					latestPostId.ToString(), 
					Ttl);
			}
		}
	}
}
