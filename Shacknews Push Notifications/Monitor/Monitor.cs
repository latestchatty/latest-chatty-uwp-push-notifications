using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using Shacknews_Push_Notifications.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications
{
	class Monitor
	{
		const int BASE_TIME_DELAY = 2;
		const double TIME_DELAY_FAIL_EXPONENT = 1.5;
		System.Threading.Timer mainTimer;
		double timeDelay = 0;
		bool timerEnabled = false;
		int lastEventId = 0;
		private readonly NotificationService notificationService;
		private readonly DatabaseService dbService;
		private CancellationTokenSource cancelToken = new CancellationTokenSource();

		public Monitor(NotificationService notificationService, DatabaseService dbService)
		{
			this.notificationService = notificationService;
			this.dbService = dbService;
		}

		public void Start()
		{
			this.timerEnabled = true;
			this.mainTimer = new System.Threading.Timer(TimerCallback, null, 0, System.Threading.Timeout.Infinite);
			Console.WriteLine("Notification monitor started.");
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
			Console.WriteLine("Notification monitor stopped.");
		}

		async private void TimerCallback(object state)
		{
			Console.WriteLine("Waiting for next monitor event...");
			try
			{
				var collection = dbService.GetCollection();

				var client = new HttpClient();

				if (this.lastEventId == 0)
				{
					var res = await client.GetAsync($"{ConfigurationManager.AppSettings["winchattyApiBase"]}getNewestEventId", this.cancelToken.Token);
					var json = JToken.Parse(await res.Content.ReadAsStringAsync());
					this.lastEventId = (int)json["eventId"];
				}

				var resEvent = await client.GetAsync($"{ConfigurationManager.AppSettings["winchattyApiBase"]}waitForEvent?lastEventId={this.lastEventId}&includeParentAuthor=1", this.cancelToken.Token);
				var jEvent = JToken.Parse(await resEvent.Content.ReadAsStringAsync());
				if (jEvent["events"] != null)
				{
					foreach (var e in jEvent["events"]) //PERF: Could probably Parallel.ForEach this.
					{
						if (e["eventType"].ToString().Equals("newPost", StringComparison.InvariantCultureIgnoreCase))
						{
							var jEventData = e["eventData"];
							var parentAuthor = jEventData["parentAuthor"].ToString();
							var latestReplyAuthor = jEventData["post"]["author"].Value<string>();
#if !DEBUG
							//Don't notify if self-reply.
							if (!parentAuthor.Equals(latestReplyAuthor, StringComparison.InvariantCultureIgnoreCase))
							{
#endif
								var user = await collection.Find(u => u.UserName.Equals(parentAuthor.ToLower())).FirstOrDefaultAsync();
								if (user != null)
								{
									if (user.NotificationInfos != null && user.NotificationInfos.Count > 0)
									{
										var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
										var update = Builders<NotificationUser>.Update
											.Inc(x => x.ReplyCount, 1)
											.CurrentDate(x => x.LastNotifiedTime);
										await collection.UpdateOneAsync(filter, update);
										user = await collection.Find(u => u.UserName.Equals(parentAuthor.ToLower())).FirstOrDefaultAsync();
										var newReplies = user.ReplyCount;
										var latestReplyText = HtmlRemoval.StripTagsRegexCompiled(jEventData["post"]["body"].Value<string>().Replace("<br />", " ").Replace(char.ConvertFromUtf32(8232), " "));
										var latestPostId = jEventData["post"]["id"];
										foreach (var info in user.NotificationInfos)
										{
											this.SendNotifications(info, newReplies, latestReplyAuthor, latestReplyText, latestPostId);
										}
										Console.WriteLine($"Would notify {parentAuthor} of {newReplies} new replies with the latest being {Environment.NewLine} {latestReplyText} by {latestReplyAuthor} with a thread id { latestPostId}");
									}
								}
								else
								{
									Console.WriteLine($"No alert on reply to {parentAuthor}");
								}
#if !DEBUG
							}
							else
							{
								Console.WriteLine($"No alert on self-reply to {parentAuthor}");
                     }
#endif
						}
					}
				}
				if (jEvent["lastEventId"] != null)
				{
					lastEventId = (int)jEvent["lastEventId"];
				}

				timeDelay = 0;
			}
			catch (Exception ex)
			{
				if (timeDelay == 0)
				{
					timeDelay = BASE_TIME_DELAY;
				}
				//There was a problem, delay further
				timeDelay = Math.Pow(timeDelay, TIME_DELAY_FAIL_EXPONENT);
				//If there was an error, reset the event ID to 0 so we get the latest, otherwise we might get stuck in a loop where the API won't return us events because there are too many.
				lastEventId = 0;
				Console.WriteLine($"!!!!!Exception in {nameof(TimerCallback)}: {ex.ToString()}");
         }
			finally
			{
				if (this.timerEnabled)
				{
					mainTimer = new System.Threading.Timer(TimerCallback, null, (int)(this.timeDelay * 1000), System.Threading.Timeout.Infinite);
				}
			}
		}

		private void SendNotifications(NotificationInfo info, int newReplies, string latestReplyAuthor, string latestReplyText, JToken latestPostId)
		{
			var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", newReplies)));
			this.notificationService.QueueNotificationData(NotificationType.Badge, info.NotificationUri, badgeDoc);

			var toastDoc = new XDocument(
				new XElement("toast", new XAttribute("launch", ""),
					new XElement("visual", 
						new XElement("binding", new XAttribute("template", "ToastText02"),
							new XElement("text", new XAttribute("id", "1"), $"Reply from {latestReplyAuthor}"),
							new XElement("text", new XAttribute("id", "2"), latestReplyText)
				))));
			this.notificationService.QueueNotificationData(NotificationType.Toast, info.NotificationUri, toastDoc);

			//this.notificationService.QueueReplyTileNotification(latestReplyAuthor, latestReplyText, info.NotificationUri);
		}


	}
}
