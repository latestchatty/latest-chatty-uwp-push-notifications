using Serilog;
using SNPN.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNPN.Common
{
	public class NotificationService : INotificationService
	{
		private readonly AccessTokenManager accessTokenManager;
		private readonly IUserRepo userRepo;
		private readonly ILogger logger;
		private readonly INetworkService networkService;
		private ConcurrentQueue<QueuedNotificationItem> queuedItems = new ConcurrentQueue<QueuedNotificationItem>();
		private int nextProcessDelay = 3000;

		private bool processingNotificationQueue;

		NotificationService(AccessTokenManager accessTokenManager, IUserRepo userRepo, ILogger logger, INetworkService networkService)
		{
			this.accessTokenManager = accessTokenManager;
			this.userRepo = userRepo;
			this.logger = logger;
			this.networkService = networkService;
		}

		public void QueueNotificationData(NotificationType type, string notificationUri, XDocument content = null, NotificationGroups group = NotificationGroups.None, string tag = null, int ttl = 0)
		{
			if (string.IsNullOrWhiteSpace(notificationUri)) throw new ArgumentNullException(nameof(notificationUri));

			if (type != NotificationType.RemoveToasts)
			{
				if (content == null) throw new ArgumentNullException(nameof(content));
			}

			var notificationItem = new QueuedNotificationItem(type, content, notificationUri, group, tag, ttl);
			this.queuedItems.Enqueue(notificationItem);
			this.StartQueueProcess();
		}
		
		Object locker = new Object();
		// [MethodImpl(MethodImplOptions.Synchronized)]
		private void StartQueueProcess()
		{
			lock (this.locker)
			{
				if (this.processingNotificationQueue) return;
				this.processingNotificationQueue = true;
			}
			Task.Run(ProcessNotificationQueue);
		}

		async private Task ProcessNotificationQueue()
		{
			try
			{
				QueuedNotificationItem notification = null;
				while (this.queuedItems.TryDequeue(out notification))
				{
					this.logger.Verbose("Processing notification queue.");
					try
					{
						var token = await this.accessTokenManager.GetAccessToken();
						var headers = new Dictionary<string, string>();

						this.logger.Information(
							"Sending notification {notificationType} with content {contentType}",
							notification.Type, notification.Content?.ToString(SaveOptions.None));
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
									result = await this.networkService.SendNotification(notification, token);
									break;
							}
							if(result.HasFlag(ResponseResult.RemoveUser))
							{
								await this.userRepo.DeleteDeviceByUri(notification.Uri);
							}
							if (result.HasFlag(ResponseResult.InvalidateToken))
							{
								this.accessTokenManager.InvalidateToken();
							}
							waitTime = (int)Math.Pow(Math.Max(waitTime, 1000), 1.1); //If we need to keep retrying, do it slower until we eventually succeed or get to high.
							if (waitTime > 10 * 60 * 1000) result = ResponseResult.FailDoNotTryAgain; //Give up after a while.
						} while (result.HasFlag(ResponseResult.FailTryAgain));
						this.nextProcessDelay = 0; //Reset on successful processing of a notification
					}
					catch (Exception ex)
					{
						this.logger.Error(ex, $"Exception in {nameof(ProcessNotificationQueue)}");
						this.nextProcessDelay = (int)Math.Pow(this.nextProcessDelay, 1.1);
					}
					finally
					{
						await Task.Delay(new TimeSpan(0, 0, 0, 0, this.nextProcessDelay));
					}
				}
			}
			finally
			{
				this.processingNotificationQueue = false;
			}
		}



	}
}
