using Newtonsoft.Json.Linq;
using Shacknews_Push_Notifications.Common;
using Shacknews_Push_Notifications.Model;
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
		private CancellationTokenSource cancelToken = new CancellationTokenSource();

		public Monitor(NotificationService notificationService, UserRepo dbService, AppConfiguration config)
		{
			this.notificationService = notificationService;
			this.dbService = dbService;
			this.configuration = config;
		}

		public void Start()
		{
			if (this.timerEnabled) return;
			this.timerEnabled = true;
			this.mainTimer = new System.Threading.Timer(TimerCallback, null, 0, System.Threading.Timeout.Infinite);
			ConsoleLog.LogMessage("Notification monitor started.");
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
			ConsoleLog.LogMessage("Notification monitor stopped.");
		}

		async private void TimerCallback(object state)
		{
			ConsoleLog.LogMessage("Waiting for next monitor event...");
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
							if (e["eventType"].ToString().Equals("newPost", StringComparison.OrdinalIgnoreCase))
							{
								var jEventData = e["eventData"];
								var parentAuthor = jEventData["parentAuthor"].Value<string>();
								var latestReplyAuthor = jEventData["post"]["author"].Value<string>();
								var latestPostId = (int)jEventData["post"]["id"];
								var postBody = HtmlRemoval.StripTagsRegexCompiled(System.Net.WebUtility.HtmlDecode(jEventData["post"]["body"].Value<string>()).Replace("<br />", " ").Replace(char.ConvertFromUtf32(8232), " "));
#if !DEBUG
								//Don't notify if self-reply.
								if (!parentAuthor.Equals(latestReplyAuthor, StringComparison.OrdinalIgnoreCase))
								{
#endif
								var usr = await this.dbService.FindUser(parentAuthor);
								if (usr != null)
								{
									this.NotifyUser(usr, latestPostId, $"Reply from {latestReplyAuthor}", postBody);
								}
								else
								{
									ConsoleLog.LogMessage($"No alert on reply to {parentAuthor}");
								}
#if !DEBUG
								}
								else
								{
									ConsoleLog.LogMessage($"No alert on self-reply to {parentAuthor}");
								}
#endif
								var users = await this.dbService.GetAllUserNames();
								foreach (var user in users)
								{
									//Pad with spaces so we don't match a partial username.
									if ((" " + postBody.ToLower() + " ").Contains(" " + user.ToLower() + " "))
									{
										var u1 = await this.dbService.FindUser(parentAuthor);
										if (u1 != null)
										{
											ConsoleLog.LogMessage($"Notifying {user} of mention by {latestReplyAuthor}");
											this.NotifyUser(u1, latestPostId, $"Mentioned by {latestReplyAuthor}", postBody);
										}
									}
								}
							}
							else
							{
								ConsoleLog.LogMessage($"Event type {e["eventType"].ToString()} not handled.");
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
					ConsoleLog.LogMessage("Timed out waiting for winchatty.");
				}
				else
				{
					//If there was an error, reset the event ID to 0 so we get the latest, otherwise we might get stuck in a loop where the API won't return us events because there are too many.
					lastEventId = 0;
					ConsoleLog.LogError($"!!!!!Exception in {nameof(TimerCallback)}: {ex.ToString()}");
				}
			}
			finally
			{
				if (this.timerEnabled)
				{
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
					ConsoleLog.LogMessage($"No notification on reply to {user.UserName} because thread is expired.");
				}
			}
		}

		private void SendNotifications(DeviceInfo info, string title, string message, int postId, int ttl)
		{
			var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", 0)));
			this.notificationService.QueueNotificationData(NotificationType.Badge, info.NotificationUri, badgeDoc);

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
