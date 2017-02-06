using Model;
using Newtonsoft.Json.Linq;
using Serilog;
using Shacknews_Push_Notifications.Common;
using Shacknews_Push_Notifications.Model;
using SNPN.Common;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications
{
	class Monitor : IDisposable
	{
		const int BASE_TIME_DELAY = 2;
		const double TIME_DELAY_FAIL_EXPONENT = 1.5;
		Timer mainTimer;
		double timeDelay = 0;
		bool timerEnabled = false;
		int lastEventId = 0;
		private readonly NotificationService notificationService;
		private readonly UserRepo dbService;
		private readonly AppConfiguration configuration;
		private readonly ILogger logger;
		private CancellationTokenSource cancelToken = new CancellationTokenSource();

		public Monitor(NotificationService notificationService, UserRepo dbService, AppConfiguration config, ILogger logger)
		{
			this.notificationService = notificationService;
			this.dbService = dbService;
			this.configuration = config;
			this.logger = logger;
		}

		public void Start()
		{
			if (this.timerEnabled) return;
			this.timerEnabled = true;
			this.mainTimer = new System.Threading.Timer(TimerCallback, null, 0, System.Threading.Timeout.Infinite);
			this.logger.Verbose("Notification monitor started.");
		}

		public void Stop()
		{
			this.timerEnabled = false;
			this.cancelToken.Cancel();
			if (this.mainTimer != null)
			{
				this.mainTimer.Dispose();
				this.mainTimer = null;
			}
			this.logger.Verbose("Notification monitor stopped.");
		}

		async private void TimerCallback(object state)
		{
			var parser = new EventParser();
			this.logger.Verbose("Waiting for next monitor event...");
			try
			{
				//var collection = dbService.GetCollection();

				using (var client = new HttpClient())
				{
					if (this.lastEventId == 0)
					{
						using (var res = await client.GetAsync($"{this.configuration.WinchattyAPIBase}getNewestEventId", this.cancelToken.Token))
						{
							var json = JToken.Parse(await res.Content.ReadAsStringAsync());
							this.lastEventId = (int)json["eventId"];
						}
					}

					JToken jEvent;
					using (var resEvent = await client.GetAsync($"{this.configuration.WinchattyAPIBase}waitForEvent?lastEventId={this.lastEventId}&includeParentAuthor=1", this.cancelToken.Token))
					{
						jEvent = JToken.Parse(await resEvent.Content.ReadAsStringAsync());
					}
					if (jEvent["events"] != null)
					{
						foreach (var e in jEvent["events"]) //PERF: Could probably Parallel.ForEach this.
						{
							var eventType = parser.GetEventType(e);
							if (eventType == EventType.NewPost)
							{
								var parsedNewPost = parser.GetNewPostEvent(e);
								var postBody = HtmlRemoval.StripTagsRegexCompiled(System.Net.WebUtility.HtmlDecode(parsedNewPost.Post.Body).Replace("<br />", " ").Replace(char.ConvertFromUtf32(8232), " "));
#if !DEBUG
								//Don't notify if self-reply.
								if (!parentAuthor.Equals(latestReplyAuthor, StringComparison.OrdinalIgnoreCase))
								{
#endif
								var usr = await this.dbService.FindUser(parsedNewPost.ParentAuthor);
								if (usr != null)
								{
									this.NotifyUser(usr, parsedNewPost.Post.Id, $"Reply from {parsedNewPost.Post.Author}", postBody);
								}
								else
								{
									this.logger.Verbose("No alert on reply to {parentAuthor}", parsedNewPost.ParentAuthor);
								}
#if !DEBUG
								}
								else
								{
									this.logger.Verbose("No alert on self-reply to {parentAuthor}", parentAuthor);
								}
#endif
								var users = await this.dbService.GetAllUserNamesForNotification();
								foreach (var user in users)
								{
									//Pad with spaces so we don't match a partial username.
									if ((" " + postBody.ToLower() + " ").Contains(" " + user.ToLower() + " "))
									{
										var u1 = await this.dbService.FindUser(parsedNewPost.ParentAuthor);
										if (u1 != null)
										{
											this.logger.Information("Notifying {user} of mention by {latestReplyAuthor}",
												user, parsedNewPost.Post.Author);
											this.NotifyUser(u1, parsedNewPost.Post.Id, $"Mentioned by {parsedNewPost.Post.Author}", postBody);
										}
									}
								}
							}
							else
							{
								this.logger.Verbose("Event type {eventType} not handled.", eventType);
							}
						}
					}
					if (jEvent["lastEventId"] != null)
					{
						lastEventId = (int)jEvent["lastEventId"];
					}
				}

				timeDelay = 0;
			}
			catch (Exception ex)
			{
				if (timeDelay == 0)
				{
					timeDelay = BASE_TIME_DELAY;
				}
				//There was a problem, delay further.  To a maximum of 3 minutes.
				timeDelay = Math.Max(Math.Pow(timeDelay, TIME_DELAY_FAIL_EXPONENT), 180);
				if (ex is TaskCanceledException)
				{
					//This is expected, we'll still slow down our polling of winchatty if the chatty's not busy but won't print a full stack.
					//Don't reset the event ID though, since nothing happened.  Don't want to miss events.
					this.logger.Verbose("Timed out waiting for winchatty.");
				}
				else
				{
					//If there was an error, reset the event ID to 0 so we get the latest, otherwise we might get stuck in a loop where the API won't return us events because there are too many.
					lastEventId = 0;
					this.logger.Error(ex, $"Exception in {nameof(TimerCallback)}");
				}
			}
			finally
			{
				if (this.timerEnabled)
				{
					this.logger.Verbose("Delaying next monitor for {monitorDelay}ms", this.timeDelay * 1000);
					mainTimer.Change((int)(this.timeDelay * 1000), Timeout.Infinite);
				}
			}
		}

		private async void NotifyUser(NotificationUser user, int latestPostId, string title, string message)
		{
			var deviceInfos = await this.dbService.GetUserDeviceInfos(user);
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

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if (this.mainTimer != null)
					{
						this.mainTimer.Dispose();
					}
					if (this.cancelToken != null)
					{
						this.cancelToken.Dispose();
					}
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~Monitor() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion


	}
}
