using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications.Common
{
	class MaintenanceService
	{
		private DatabaseService dbService;
		private NotificationService notificationService;
		private Timer mainTimer;
		private bool timerEnabled = false;
		private bool timerRunning;

		public MaintenanceService(DatabaseService dbService, NotificationService notificationService)
		{
			this.dbService = dbService;
			this.notificationService = notificationService;
		}

		public void Start()
		{
			if (timerEnabled) return;
			timerEnabled = true;
			this.mainTimer = new Timer(TimerCallback, null, 0, 30000);
			Console.WriteLine("Maintenance service started.");
		}

		public void Stop()
		{
			if (this.mainTimer != null)
			{
				this.mainTimer.Dispose();
				this.mainTimer = null;
			}
			timerEnabled = false;
			Console.WriteLine("Maintenance service stopped.");
		}
		async private void TimerCallback(object state)
		{
			try
			{
				if(this.timerRunning)
				{
					Console.WriteLine("Skipping maintenance task because it's currently running.");
					return;
				}
				this.timerRunning = true;
				Console.WriteLine("Running maintenance task.");
				var collection = dbService.GetCollection();
				var allUsers = await collection.Find(new MongoDB.Bson.BsonDocument()).ToListAsync();
				foreach (var user in allUsers)
				{
					//If there's nothing to do, skip the user.
					if (user.ReplyEntries == null || user.ReplyEntries.Count == 0) continue;

					var originalEntryCount = user.ReplyEntries.Count;

					//First, remove any expired entries.
					var oldEntryCount = user.ReplyEntries.Count;
					var newEntries = user.ReplyEntries.Where(e => e.Expiration > DateTime.UtcNow).ToList();
					var expiredCount = oldEntryCount - newEntries.Count;
					var alreadySeenCount = 0;
					oldEntryCount = newEntries.Count;

					//Next, remove any entries that might have been added because the user clicked on a post before the notification got created.
					var seenPosts = await this.GetSeenPostIds(user.UserName);
					if(seenPosts != null)
					{
						newEntries = newEntries.Where(e => !seenPosts.Contains(e.PostId)).ToList();
						alreadySeenCount = oldEntryCount - newEntries.Count;
					}

					if (originalEntryCount != newEntries.Count)
					{
						Console.WriteLine($"Removing {expiredCount} expired and {alreadySeenCount} already seen entries from {user.UserName}");
						var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
						var update = Builders<NotificationUser>.Update
							.Set(x => x.ReplyEntries, newEntries)
							.CurrentDate(x => x.DateUpdated);
						await collection.UpdateOneAsync(filter, update);

						var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", newEntries.Count)));
						await this.notificationService.QueueNotificationToUser(NotificationType.Badge, badgeDoc, user.UserName);
					}
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine($"Exception in maintenance task : {ex}");
			}
			finally
			{
				Console.WriteLine("Maintenance complete.");
				this.timerRunning = false;
			}
		}

		private async Task<List<int>> GetSeenPostIds(string userName)
		{
			try
			{
				var handler = new HttpClientHandler();
				if (handler.SupportsAutomaticDecompression)
				{
					handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				}

				using (var request = new HttpClient(handler, true))
				{
					using (var response = await request.GetAsync($"{ConfigurationManager.AppSettings["winChattyApiBase"]}clientData/getClientData?username={Uri.EscapeUriString(userName)}&client=latestchattyUWP{Uri.EscapeUriString("SeenPosts")}"))
					{
						var resString = await response.Content.ReadAsStringAsync();
						var jResponse = JToken.Parse(resString);
						var data = jResponse["data"].ToString();
						if (!string.IsNullOrWhiteSpace(data))
						{
							return Newtonsoft.Json.JsonConvert.DeserializeObject<List<int>>(data);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving post ids for {userName} : {ex}");
         }
			return null;
		}
	}
}
