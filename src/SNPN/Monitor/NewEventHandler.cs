﻿using Serilog;
using SNPN.Common;
using SNPN.Data;
using SNPN.Model;
using System;
using System.Collections.Generic;
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

			var paddedBody = (" " + postBody.ToLower() + " ");
			var users = await _userRepo.GetAllUserNamesForNotification();
			foreach (var user in users)
			{
				//Don't notify a user of their own posts.
				if (user.ToLower().Equals(e.Post.Author.ToLower())) continue;
				//Pad with spaces so we don't match a partial username.
				if (paddedBody.Contains(" " + user.ToLower() + " "))
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

			var words = await _userRepo.GetAllWordsForNotifications();
			var sentNotifications = new Dictionary<int, List<string>>();
			foreach (var word in words)
			{
				if (paddedBody.Contains(" " + word.Word + " "))
				{
					var usersToNotify = await _userRepo.FindUsersByWord(word.Id);
					foreach (var userToNotify in usersToNotify)
					{
						//Don't notify a user of their own posts.
						if (userToNotify.UserName.ToLower().Equals(e.Post.Author.ToLower())) continue;
						if (sentNotifications.ContainsKey(e.Post.Id))
						{
							//Don't notify if we've already notified the user on this post id because of another keyword
							if (sentNotifications[e.Post.Id].Contains(userToNotify.UserName))
							{
								_logger.Verbose("Won't notify {user} of {keyword} because they've already been notified for {postId}", userToNotify.UserName, word.Word, e.Post.Id);
								continue;
							}
							//Otherwise add them
							sentNotifications[e.Post.Id].Add(userToNotify.UserName);
						}
						else
						{
							sentNotifications.Add(e.Post.Id, new List<string> { userToNotify.UserName });
						}
						_logger.Information("Notifying {user} of {keyword} on {postBody}", userToNotify.UserName, word.Word, postBody);
						NotifyUser(userToNotify, e.Post.Id, $"Keyword '{word.Word}' used by {e.Post.Author}", postBody);
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
