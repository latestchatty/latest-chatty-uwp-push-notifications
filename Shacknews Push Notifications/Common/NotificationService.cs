using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications.Common
{
	public class NotificationService
	{
		private readonly AccessTokenManager accessTokenManager;
		private readonly DatabaseService dbService;
		private System.Threading.Timer processTimer = null;
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
			public QueuedNotificationItem(NotificationType type, XDocument content, string uri = null)
			{
				this.Type = type;
				this.Content = content;
				this.Uri = uri;
			}

			public XDocument Content { get; private set; }
			public NotificationType Type { get; private set; }
			public string Uri { get; private set; }
		}

		private Dictionary<NotificationType, string> notificationTypeMapping = new Dictionary<NotificationType, string>
		{
			{ NotificationType.Badge, "wns/badge" },
			{ NotificationType.Tile, "wns/tile" },
			{ NotificationType.Toast, "wns/toast" }
		};


		public NotificationService(AccessTokenManager accessTokenManager, DatabaseService dbService)
		{
			this.accessTokenManager = accessTokenManager;
			this.dbService = dbService;
		}

		public void QueueNotificationData(NotificationType type, string notificationUri, XDocument content = null)
		{
			if (string.IsNullOrWhiteSpace(notificationUri)) throw new ArgumentNullException(nameof(notificationUri));

			if (type != NotificationType.RemoveAllToasts)
			{
				if (content == null) throw new ArgumentNullException(nameof(content));
			}

			var notificationItem = new QueuedNotificationItem(type, content, notificationUri);
			this.queuedItems.Enqueue(notificationItem);
			this.StartQueueProcess();
		}

		async public Task QueueNotificationToUser(NotificationType type, XDocument content, string userName)
		{
			var collection = dbService.GetCollection();
			var user = await collection.Find(u => u.UserName.Equals(userName.ToLower())).FirstOrDefaultAsync();
			if (user != null)
			{
				foreach (var info in user.NotificationInfos)
				{
					this.QueueNotificationData(type, info.NotificationUri, content);
				}
			}
		}

		async public Task RemoveAllToastsForUser(string userName)
		{
			var collection = dbService.GetCollection();
			var user = await collection.Find(u => u.UserName.Equals(userName.ToLower())).FirstOrDefaultAsync();
			if (user != null)
			{
				foreach (var info in user.NotificationInfos)
				{
					this.QueueNotificationData(NotificationType.RemoveAllToasts, info.NotificationUri);
				}
			}
		}

		private HttpClient CreateClient(string token)
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.ExpectContinue = false;
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
			return client;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private void StartQueueProcess()
		{
			if (this.processTimer == null)
			{
				this.processTimer = new System.Threading.Timer(async x => await ProcessNotificationQueue(), null, 0, System.Threading.Timeout.Infinite);
			}
		}

		async private Task ProcessNotificationQueue()
		{
			try
			{
				Console.WriteLine("Processing notification queue.");
				QueuedNotificationItem notification = null;
				while (this.queuedItems.TryDequeue(out notification))
				{
					var token = await this.accessTokenManager.GetAccessToken();
					var client = this.CreateClient(token);
					Console.WriteLine($"Sending notification {notification.Type} with content { notification.Content?.ToString(SaveOptions.None) }");
					var waitTime = 0;
					ResponseResult result;
					do
					{
						await Task.Delay(waitTime);
						HttpResponseMessage response = null;
						switch (notification.Type)
						{
							case NotificationType.Badge:
							case NotificationType.Tile:
							case NotificationType.Toast:
								client.DefaultRequestHeaders.Add("X-WNS-Type", this.notificationTypeMapping[notification.Type]);
								var stringContent = new StringContent(notification.Content.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
								response = await client.PostAsync(notification.Uri, stringContent);
								break;
							case NotificationType.RemoveAllToasts:
								client.DefaultRequestHeaders.Add("X-WNS-Match", "type=wns/toast;all");
								response = await client.DeleteAsync(notification.Uri);
								break;
						}
						result = await this.ProcessResponse(response, notification.Uri);
						waitTime = (int)Math.Pow(Math.Max(waitTime, 1000), 1.1); //If we need to keep retrying, do it slower until we eventually succeed or get to high.
						if (waitTime > 10 * 60 * 1000) result = ResponseResult.FailDoNotTryAgain; //Give up after a while.
					} while (result == ResponseResult.FailTryAgain);
				}
				if(notification != null)
				{
					this.nextProcessDelay = 3000; //Reset on successful processing of queue
				}
				Console.WriteLine("Notification Queue empty.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"!!!!!!Exception in {nameof(ProcessNotificationQueue)} : {ex.ToString()}");
				this.nextProcessDelay = (int)Math.Pow(this.nextProcessDelay, 1.1);
			}
			finally
			{
				//Process again after delay.
				this.processTimer = new System.Threading.Timer(async x => await ProcessNotificationQueue(), null, this.nextProcessDelay, System.Threading.Timeout.Infinite);
			}
		}

		async private Task<ResponseResult> ProcessResponse(HttpResponseMessage response, string uri)
		{
			//By default, we'll just let it die if we don't know specifically that we can try again.
			ResponseResult result = ResponseResult.FailDoNotTryAgain;

			switch (response.StatusCode)
			{
				case System.Net.HttpStatusCode.OK:
					result = ResponseResult.Success;
					break;
				case System.Net.HttpStatusCode.NotFound:
				case System.Net.HttpStatusCode.Gone:
					//Invalid Uri or expired, remove it from the DB
					var collection = this.dbService.GetCollection();
					var user = await collection.Find(u => u.NotificationInfos.Any(ni => ni.NotificationUri.Equals(uri))).FirstOrDefaultAsync();
					if (user != null)
					{
						var infos = user.NotificationInfos;
						var infoToRemove = infos.SingleOrDefault(x => x.NotificationUri.Equals(uri));
						if (infoToRemove != null)
						{
							infos.Remove(infoToRemove);

							var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
							var update = Builders<NotificationUser>.Update
								.CurrentDate(x => x.DateUpdated)
								.Set(x => x.NotificationInfos, infos);
							await collection.UpdateOneAsync(filter, update);
						}
					}
					break;
				case System.Net.HttpStatusCode.NotAcceptable:
					result = ResponseResult.FailTryAgain;
					break;
				default:
					break;
			}
			return result;
		}
	}
}
