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

		public NewEventHandler(INotificationService notificationService, IUserRepo userRepo, ILogger logger)
		{
			this.notificationService = notificationService;
			this.userRepo = userRepo;
			this.logger = logger;
		}

		public async Task ProcessEvent(NewPostEvent e)
		{
			var postBody = HtmlRemoval.StripTagsRegexCompiled(System.Net.WebUtility.HtmlDecode(e.Post.Body).Replace("<br />", " ").Replace(char.ConvertFromUtf32(8232), " "));
#if !DEBUG
			//Don't notify if self-reply.
			if (!e.ParentAuthor.Equals(e.Post.Author, StringComparison.OrdinalIgnoreCase))
			{
#endif
				var usr = await this.userRepo.FindUser(e.ParentAuthor);
				if (usr != null)
				{
					this.NotifyUser(usr, e.Post.Id, $"Reply from {e.Post.Author}", postBody);
				}
				else
				{
					this.logger.Verbose("No alert on reply to {parentAuthor}", e.ParentAuthor);
				}
#if !DEBUG
			}
			else
			{
				this.logger.Verbose("No alert on self-reply to {parentAuthor}", e.ParentAuthor);
			}
#endif
			var users = await this.userRepo.GetAllUserNamesForNotification();
			foreach (var user in users)
			{
				//Pad with spaces so we don't match a partial username.
				if ((" " + postBody.ToLower() + " ").Contains(" " + user.ToLower() + " "))
				{
					var u1 = await this.userRepo.FindUser(e.ParentAuthor);
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
			if (deviceInfos != null && deviceInfos.Count() > 0)
			{
				TimeSpan ttl = new TimeSpan(48, 0, 0);

				var expireDate = DateTime.UtcNow.Add(ttl);

				if (expireDate > DateTime.UtcNow)
				{
					foreach (var info in deviceInfos)
					{
						this.SendNotifications(info, title, message, latestPostId, (int)ttl.TotalSeconds);
					}
				}
				else
				{
					this.logger.Information("No notification on reply to {userName} because thread is expired.", user.UserName);
				}
			}
		}

		private void SendNotifications(DeviceInfo info, string title, string message, int postId, int ttl)
		{
			var toastDoc = new XDocument(
				 new XElement("toast", new XAttribute("launch", $"goToPost?postId={postId}"),
					  new XElement("visual",
							new XElement("binding", new XAttribute("template", "ToastText02"),
								 new XElement("text", new XAttribute("id", "1"), title),
								 new XElement("text", new XAttribute("id", "2"), message)
							)
					  ),
					  new XElement("actions",
								 new XElement("input", new XAttribute("id", "message"),
									  new XAttribute("type", "text"),
									  new XAttribute("placeHolderContent", "reply")),
								 new XElement("action", new XAttribute("activationType", "background"),
									  new XAttribute("content", "reply"),
									  new XAttribute("arguments", $"reply={postId}")/*,
								new XAttribute("imageUri", "Assets/success.png"),
								new XAttribute("hint-inputId", "message")*/)
					  )
				 )
			);
			this.notificationService.QueueNotificationData(NotificationType.Toast, info.NotificationUri, toastDoc, NotificationGroups.ReplyToUser, postId.ToString(), ttl);

			//this.notificationService.QueueReplyTileNotification(latestReplyAuthor, latestReplyText, info.NotificationUri);
		}
	}
}
