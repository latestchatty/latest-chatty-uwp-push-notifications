using Serilog;
using SNPN.Common;
using SNPN.Data;
using SNPN.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNPN.Monitor
{
	public class NewEventHandler
	{
		private readonly ILogger logger;
		private readonly INotificationService notificationService;
		private readonly IUserRepo userRepo;
		private const int TTL = 172800; // 48 hours

		public NewEventHandler(INotificationService notificationService, IUserRepo userRepo, ILogger logger)
		{
			this.notificationService = notificationService;
			this.userRepo = userRepo;
			this.logger = logger;
		}

		public async Task ProcessEvent(NewPostEvent e)
		{
			var postBody = HtmlRemoval.StripTagsRegexCompiled(System.Net.WebUtility.HtmlDecode(e.Post.Body).Replace("<br />", " ").Replace(char.ConvertFromUtf32(8232), " "));
			//Don't notify if self-reply.
			if (!e.ParentAuthor.Equals(e.Post.Author, StringComparison.OrdinalIgnoreCase))
			{
				var usr = await this.userRepo.FindUser(e.ParentAuthor);
				if (usr != null)
				{
					this.NotifyUser(usr, e.Post.Id, $"Reply from {e.Post.Author}", postBody);
				}
				else
				{
					this.logger.Verbose("No alert on reply to {parentAuthor}", e.ParentAuthor);
				}
			}
			else
			{
				this.logger.Verbose("No alert on self-reply to {parentAuthor}", e.ParentAuthor);
			}

			var users = await this.userRepo.GetAllUserNamesForNotification();
			foreach (var user in users)
			{
				//Pad with spaces so we don't match a partial username.
				if ((" " + postBody.ToLower() + " ").Contains(" " + user.ToLower() + " "))
				{
					var u1 = await this.userRepo.FindUser(user);
					if (u1 != null)
					{
						this.logger.Information("Notifying {user} of mention by {latestReplyAuthor}",
							user, e.Post.Author);
						this.NotifyUser(u1, e.Post.Id, $"Mentioned by {e.Post.Author}", postBody);
					}
				}
			}
		}

		private async void NotifyUser(NotificationUser user, int latestPostId, string title, string message)
		{
			var deviceInfos = await this.userRepo.GetUserDeviceInfos(user);
			if (!deviceInfos.Any()) return;

			foreach (var info in deviceInfos)
			{
				var toastDoc = NotificationBuilder.BuildReplyDoc(latestPostId, title, message);
				this.notificationService.QueueNotificationData(
					NotificationType.Toast, 
					info.NotificationUri, 
					toastDoc, 
					NotificationGroups.ReplyToUser, 
					latestPostId.ToString(), 
					TTL);
			}
		}
	}
}
