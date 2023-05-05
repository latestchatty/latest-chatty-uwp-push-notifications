﻿using Serilog;
using SNPN.Data;
using SNPN.Model;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNPN.Common
{
	public class NotificationService : INotificationService
	{
		private readonly AccessTokenManager _accessTokenManager;
		private readonly IUserRepo _userRepo;
		private readonly ILogger _logger;
		private readonly INetworkService _networkService;
		private readonly ConcurrentQueue<QueuedNotificationItem> _queuedItems = new ConcurrentQueue<QueuedNotificationItem>();
		private int _nextProcessDelay = 3000;

		private bool _processingNotificationQueue;

		public NotificationService(AccessTokenManager accessTokenManager, IUserRepo userRepo, ILogger logger, INetworkService networkService)
		{
			_accessTokenManager = accessTokenManager;
			_userRepo = userRepo;
			_logger = logger;
			_networkService = networkService;
		}

		public void QueueNotificationData(NotificationType type, string notificationUri, Post post, NotificationMatchType matchType, string title, string message, NotificationGroups group = NotificationGroups.None, int ttl = 0)
		{
			var postId = post.Id;
			if (string.IsNullOrWhiteSpace(notificationUri)) throw new ArgumentNullException(nameof(notificationUri));
			var content = NotificationBuilder.BuildReplyDoc(postId, title, message);

			if (type != NotificationType.RemoveToasts)
			{
				if (content == null) throw new ArgumentNullException(nameof(content));
			}

			var notificationItem = new QueuedNotificationItem(type, content, post, matchType, notificationUri, group, postId.ToString(), ttl, title, message);
			_queuedItems.Enqueue(notificationItem);
			StartQueueProcess();
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void StartQueueProcess()
		{
			if (_processingNotificationQueue) return;
			_processingNotificationQueue = true;
			Task.Run(ProcessNotificationQueue);
		}

		private async Task ProcessNotificationQueue()
		{
			try
			{
				while (_queuedItems.TryDequeue(out var notification))
				{
					_logger.Verbose("Processing notification queue.");
					try
					{
						var token = await _accessTokenManager.GetAccessToken();

						_logger.Information(
							"Sending notification {notificationType}",
							notification.Type);
						_logger.Debug(
							"With content {contentType}",
							notification.Content?.ToString(SaveOptions.None));
						var waitTime = 0;
						ResponseResult result = ResponseResult.FailDoNotTryAgain;
						do
						{
							await Task.Delay(waitTime);
							switch (notification.Type)
							{
								case NotificationType.Badge:
								case NotificationType.Tile:
								case NotificationType.Toast:
									if(notification.Uri.StartsWith("fcm://")) {
										result = await _networkService.SendNotificationFCM(notification);
									}
									else {
										result = await _networkService.SendNotificationWNS(notification, token);
									}
									break;
							}
							if (result.HasFlag(ResponseResult.RemoveUser))
							{
								await _userRepo.DeleteDeviceByUri(notification.Uri);
							}
							if (result.HasFlag(ResponseResult.InvalidateToken))
							{
								_accessTokenManager.InvalidateToken();
							}
							waitTime = (int)Math.Pow(Math.Max(waitTime, 1000), 1.1); //If we need to keep retrying, do it slower until we eventually succeed or get to high.
							if (waitTime > 10 * 60 * 1000) result = ResponseResult.FailDoNotTryAgain; //Give up after a while.
						} while (result.HasFlag(ResponseResult.FailTryAgain));
						_nextProcessDelay = 0; //Reset on successful processing of a notification
					}
					catch (Exception ex)
					{
						_logger.Error(ex, $"Exception in {nameof(ProcessNotificationQueue)}");
						_nextProcessDelay = (int)Math.Pow(_nextProcessDelay, 1.1);
					}
					finally
					{
						await Task.Delay(new TimeSpan(0, 0, 0, 0, _nextProcessDelay));
					}
				}
			}
			finally
			{
				_processingNotificationQueue = false;
			}
		}
	}
}
