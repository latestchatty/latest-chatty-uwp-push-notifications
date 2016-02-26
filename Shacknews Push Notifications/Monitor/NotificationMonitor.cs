using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
		string accessToken = string.Empty;

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

		async private Task GetAccessToken()
		{
			Console.WriteLine("Getting access token.");
			var client = new HttpClient();
			var data = new FormUrlEncodedContent(new Dictionary<string, string> {
					{ "grant_type", "client_credentials" },
					{ "client_id", ConfigurationManager.AppSettings["notificationSID"] },
					{ "client_secret", ConfigurationManager.AppSettings["clientSecret"] },
					{ "scope", "notify.windows.com" },
				});
			var response = await client.PostAsync("https://login.live.com/accesstoken.srf", data);
			if (response.StatusCode == System.Net.HttpStatusCode.OK)
			{
				var responseJson = JToken.Parse(await response.Content.ReadAsStringAsync());
				if (responseJson["access_token"] != null)
				{
					this.accessToken = responseJson["access_token"].Value<string>();
					Console.WriteLine($"Got access token {this.accessToken}");
				}
			}
		}

		async private void TimerCallback(object state)
		{
			Console.WriteLine("Notification timer.");
			try
			{
				if (string.IsNullOrWhiteSpace(this.accessToken))
				{
					await this.GetAccessToken();
				}

				var dbClient = new MongoClient(ConfigurationManager.AppSettings["dbConnectionString"]);
				var db = dbClient.GetDatabase("notifications");

				var collection = db.GetCollection<NotificationUser>("notificationUsers");

				var client = new HttpClient();

				if (this.lastEventId == 0)
				{
					var res = await client.GetAsync($"{ConfigurationManager.AppSettings["winchattyApiBase"]}getNewestEventId");
					var json = JToken.Parse(await res.Content.ReadAsStringAsync());
					this.lastEventId = (int)json["eventId"];
					this.lastEventId -= 100;
				}

				var resEvent = await client.GetAsync($"{ConfigurationManager.AppSettings["winchattyApiBase"]}waitForEvent?lastEventId={this.lastEventId}&includeParentAuthor=1");
				var jEvent = JToken.Parse(await resEvent.Content.ReadAsStringAsync());
				if (jEvent["events"] != null)
				{
					foreach (var e in jEvent["events"])
					{
						if (e["eventType"].ToString().Equals("newPost", StringComparison.InvariantCultureIgnoreCase))
						{
							var jEventData = e["eventData"];
							var parentAuthor = jEventData["parentAuthor"].ToString();
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
									var latestReplyAuthor = jEventData["post"]["author"].Value<string>();
									var latestReplyText = jEventData["post"]["body"].Value<string>(); //TODO: Sanitize
									var latestPostId = jEventData["post"]["id"];
									foreach (var info in user.NotificationInfos)
									{
										await this.SendNotifications(info, newReplies, latestReplyAuthor, latestReplyText, latestPostId);
									}
									Console.WriteLine($"Would notify {parentAuthor} of {newReplies} new replies with the latest being {Environment.NewLine} {latestReplyText} by {latestReplyAuthor} with a thread id { latestPostId}");
								}
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
				if (timeDelay == 0)
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

		async private Task SendNotifications(NotificationInfo info, int newReplies, string latestReplyAuthor, string latestReplyText, JToken latestPostId)
		{
			if (string.IsNullOrWhiteSpace(this.accessToken)) return;
			
			//var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", newReplies)));
			//await this.SendNotificationData("wns/badge", badgeDoc, info.NotificationUri);

			var toastDoc = new XDocument(
				new XElement("toast", new XAttribute("launch", ""),
					new XElement("visual", 
						new XElement("binding", new XAttribute("template", "ToastText02"),
							new XElement("text", new XAttribute("id", "1"), $"Reply from {latestReplyAuthor}"),
							new XElement("text", new XAttribute("id", "2"), latestReplyText)
				))));
			await this.SendNotificationData("wns/toast", toastDoc, info.NotificationUri);

			var tileDoc = new XDocument(
				new XElement("tile",
					new XElement("visual", new XAttribute("version", "2"),
						new XElement("binding", new XAttribute("template", "TileWide310x150Text09"), new XAttribute("fallback", "TileWideText09"),
							new XElement("text", new XAttribute("id", "1"), $"Reply from {latestReplyAuthor}"),
							new XElement("text", new XAttribute("id", "2"), latestReplyText)))));

			await this.SendNotificationData("wns/tile", tileDoc, info.NotificationUri);

			tileDoc = new XDocument(
				new XElement("tile",
					new XElement("visual", new XAttribute("version", "2"),
						new XElement("binding", new XAttribute("template", "TileSquare150x150Text02"), new XAttribute("fallback", "TileSquareText02"),
							new XElement("text", new XAttribute("id", "1"), $"Reply from {latestReplyAuthor}"),
							new XElement("text", new XAttribute("id", "2"), latestReplyText)))));

			await this.SendNotificationData("wns/tile", tileDoc, info.NotificationUri);

			tileDoc = new XDocument(
				new XElement("tile",
					new XElement("visual", new XAttribute("version", "2"),
						new XElement("binding", new XAttribute("template", "TileSquare310x310TextList03"),
							new XElement("text", new XAttribute("id", "1"), $"Reply from {latestReplyAuthor}"),
							new XElement("text", new XAttribute("id", "2"), latestReplyText),
							new XElement("text", new XAttribute("id", "3")),
							new XElement("text", new XAttribute("id", "4")),
							new XElement("text", new XAttribute("id", "5")),
							new XElement("text", new XAttribute("id", "6"))))));
			
			await this.SendNotificationData("wns/tile", tileDoc, info.NotificationUri);
		}

		async private Task SendNotificationData(string type, XDocument content, string notificationUri)
		{
			var client = new HttpClient();
			var stringContent = new StringContent(content.ToString(SaveOptions.DisableFormatting), ASCIIEncoding.UTF8, "text/xml");
			client.DefaultRequestHeaders.Add("X-WNS-Type", type);
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.accessToken}");
			var response = await client.PostAsync(notificationUri, stringContent);
		}
	}
}
