using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications.Common
{
	public class NotificationService
	{
		private readonly AccessTokenManager accessTokenManager;
		private readonly UserRepo dbService;
		private ConcurrentQueue<QueuedNotificationItem> queuedItems = new ConcurrentQueue<QueuedNotificationItem>();
		private int nextProcessDelay = 3000;

		private enum ResponseResult
		{
			Success,
			FailDoNotTryAgain,
			FailTryAgain
		}

		private class QueuedNotificationItem
		{
			public QueuedNotificationItem(NotificationType type, XDocument content, string uri = null, NotificationGroups group = NotificationGroups.None, string tag = null, int ttl = 0)
			{
				this.Type = type;
				this.Content = content;
				this.Uri = uri;
				this.Group = group;
				this.Tag = tag;
				this.TTL = ttl;
			}

			public XDocument Content { get; private set; }
			public NotificationType Type { get; private set; }
			public string Uri { get; private set; }
			public NotificationGroups Group { get; private set; }
			public string Tag { get; private set; }
			public int TTL { get; private set; }
		}

		private Dictionary<NotificationType, string> notificationTypeMapping = new Dictionary<NotificationType, string>
		  {
				{ NotificationType.Badge, "wns/badge" },
				{ NotificationType.Tile, "wns/tile" },
				{ NotificationType.Toast, "wns/toast" }
		  };
		private bool processingNotificationQueue;

		public NotificationService(AccessTokenManager accessTokenManager, UserRepo dbService)
		{
			this.accessTokenManager = accessTokenManager;
			this.dbService = dbService;
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

		private HttpClient CreateClient(string token)
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.ExpectContinue = false;
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
			return client;
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
					ConsoleLog.LogMessage("Processing notification queue.");
					try
					{
						var token = await this.accessTokenManager.GetAccessToken();
						using (var client = this.CreateClient(token))
						{
							ConsoleLog.LogMessage($"Sending notification {notification.Type} with content { notification.Content?.ToString(SaveOptions.None) }");
							var waitTime = 0;
							ResponseResult result;
							do
							{
								await Task.Delay(waitTime);
								HttpResponseMessage response = null;
								try
								{
									switch (notification.Type)
									{
										case NotificationType.Badge:
										case NotificationType.Tile:
										case NotificationType.Toast:
											client.DefaultRequestHeaders.Add("X-WNS-Type", this.notificationTypeMapping[notification.Type]);
											if (notification.Group != NotificationGroups.None)
											{
												client.DefaultRequestHeaders.Add("X-WNS-Group", Uri.EscapeUriString(Enum.GetName(typeof(NotificationGroups), notification.Group)));
											}
											if (!string.IsNullOrWhiteSpace(notification.Tag))
											{
												client.DefaultRequestHeaders.Add("X-WNS-Tag", Uri.EscapeUriString(notification.Tag));
											}
											if (notification.TTL > 0)
											{
												client.DefaultRequestHeaders.Add("X-WNS-TTL", notification.TTL.ToString());
											}
											using (var stringContent = new StringContent(notification.Content.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml"))
											{
												response = await client.PostAsync(notification.Uri, stringContent);
											}
											break;
										case NotificationType.RemoveToasts:
											var match = string.Empty;
											if (notification.Group != NotificationGroups.None)
											{
												match = $"group={Uri.EscapeUriString(Enum.GetName(typeof(NotificationGroups), notification.Group))}";
											}
											if (!string.IsNullOrWhiteSpace(notification.Tag))
											{
												if (!string.IsNullOrWhiteSpace(match))
												{
													match += ";";
												}
												match += $"tag={Uri.EscapeUriString(notification.Tag)}";
											}
											if (string.IsNullOrWhiteSpace(match))
											{
												match = "all";
											}
											client.DefaultRequestHeaders.Add("X-WNS-Match", $"type=wns/toast;{match}");
											response = await client.DeleteAsync(notification.Uri);
											break;
									}
									result = await this.ProcessResponse(response, notification.Uri);
								}
								finally
								{
									if (response != null)
									{
										response.Dispose();
									}
								}
								waitTime = (int)Math.Pow(Math.Max(waitTime, 1000), 1.1); //If we need to keep retrying, do it slower until we eventually succeed or get to high.
								if (waitTime > 10 * 60 * 1000) result = ResponseResult.FailDoNotTryAgain; //Give up after a while.
							} while (result == ResponseResult.FailTryAgain);

						}
						this.nextProcessDelay = 0; //Reset on successful processing of a notification
					}
					catch (Exception ex)
					{
						ConsoleLog.LogError($"!!!!!!Exception in {nameof(ProcessNotificationQueue)} : {ex.ToString()}");
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


		async private Task<ResponseResult> ProcessResponse(HttpResponseMessage response, string uri)
		{
			//By default, we'll just let it die if we don't know specifically that we can try again.
			ResponseResult result = ResponseResult.FailDoNotTryAgain;
			ConsoleLog.LogMessage($"Notification Response Code: {response.StatusCode}");
			switch (response.StatusCode)
			{
				case System.Net.HttpStatusCode.OK:
					result = ResponseResult.Success;
					break;
				case System.Net.HttpStatusCode.NotFound:
				case System.Net.HttpStatusCode.Gone:
				case System.Net.HttpStatusCode.Forbidden:
					await this.dbService.DeleteDeviceByUri(uri);
					break;
				case System.Net.HttpStatusCode.NotAcceptable:
					result = ResponseResult.FailTryAgain;
					break;
				case System.Net.HttpStatusCode.Unauthorized:
					//Need to refresh the token, so invalidate it and we'll pick up a new one on retry.
					this.accessTokenManager.InvalidateToken();
					result = ResponseResult.FailTryAgain;
					break;
				default:
					break;
			}
			return result;
		}
	}
}
