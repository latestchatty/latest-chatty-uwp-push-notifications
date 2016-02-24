using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications
{
	class NotificationMonitor
	{
		const int BASE_TIME_DELAY = 2;
		const double TIME_DELAY_FAIL_EXPONENT = 1.5;
		System.Threading.Timer mainTimer;
		double timeDelay = 0;
		bool timerEnabled = false;
		int lastEventId = 0;

		string cloudApiBase = "https://winchatty.com/v2/"; //TODO: Read from configuration

		public void Start()
		{
			this.timerEnabled = true;
			this.mainTimer = new System.Threading.Timer(TimerCallback, null, 0, System.Threading.Timeout.Infinite);
			Console.WriteLine("Notification monitor started.");
		}

		public void Stop()
		{
			this.timerEnabled = false;
			if (this.mainTimer != null)
			{
				this.mainTimer.Dispose();
				this.mainTimer = null;
			}
			Console.WriteLine("Notification monitor stopped.");
		}

		async private void TimerCallback(object state)
		{
			Console.WriteLine("Notification timer.");
			try
			{
				var dbClient = new MongoClient("mongodb://192.168.1.216:27017/"); //TODO: Read from configuration
				var db = dbClient.GetDatabase("notifications");

				var collection = db.GetCollection<NotificationUser>("notificationUsers");

				var client = new HttpClient();

				if (this.lastEventId == 0)
				{
					var res = await client.GetAsync($"{this.cloudApiBase}getNewestEventId");
					var json = JToken.Parse(await res.Content.ReadAsStringAsync());
					this.lastEventId = (int)json["eventId"];
					this.lastEventId -= 100;
				}

				var resEvent = await client.GetAsync($"{this.cloudApiBase}waitForEvent?lastEventId={this.lastEventId}&includeParentAuthor=1");
				var jEvent = JToken.Parse(await resEvent.Content.ReadAsStringAsync());
				if (jEvent["events"] != null)
				{
					foreach (var e in jEvent["events"])
					{
						//e.ToString().Dump();
						if (e["eventType"].ToString().Equals("newPost", StringComparison.InvariantCultureIgnoreCase))
						{
							var jEventData = e["eventData"];
							var parentAuthor = jEventData["parentAuthor"].ToString();
							var user = await collection.Find(u => u.UserName.Equals(parentAuthor.ToLower())).FirstOrDefaultAsync();
							if (user != null)
							{
								var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
								var update = Builders<NotificationUser>.Update
									.Inc(x => x.ReplyCount, 1)
									.CurrentDate(x => x.LastNotifiedTime);
								await collection.UpdateOneAsync(filter, update);
								user = await collection.Find(u => u.UserName.Equals(parentAuthor.ToLower())).FirstOrDefaultAsync();
								var newReplies = user.ReplyCount;
								var latestReplyAuthor = jEventData["post"]["author"];
								var latestReplyText = jEventData["post"]["body"]; //TODO: Sanitize
								var latestPostId = jEventData["post"]["id"];
								Console.WriteLine($"Would notify {parentAuthor} of {newReplies} new replies with the latest being {Environment.NewLine} {latestReplyText} by {latestReplyAuthor} with a thread id { latestPostId}");
							}
							else
							{
								//Console.WriteLine($"No alert on reply to {parentAuthor}");
							}
						}
					}
				}
				if (jEvent["lastEventId"] != null)
				{
					lastEventId = (int)jEvent["lastEventId"];
				}

				timeDelay = 0;
			}
			catch
			{
				if(timeDelay == 0)
				{
					timeDelay = BASE_TIME_DELAY;
				}
				//There was a problem, delay further
				timeDelay = Math.Pow(timeDelay, TIME_DELAY_FAIL_EXPONENT);
			}
			finally
			{
				if (this.timerEnabled)
				{
					mainTimer = new System.Threading.Timer(TimerCallback, null, (int)(this.timeDelay * 1000), System.Threading.Timeout.Infinite);
				}
			}
		}
	}
}
