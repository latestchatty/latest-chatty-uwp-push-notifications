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
	class MaintenanceService : IDisposable
	{
		private DatabaseService dbService;
		private NotificationService notificationService;
		private Timer mainTimer;
		private bool timerEnabled = false;

		public MaintenanceService(DatabaseService dbService, NotificationService notificationService)
		{
			this.dbService = dbService;
			this.notificationService = notificationService;
		}

		public void Start()
		{
			if (timerEnabled) return;
			timerEnabled = true;
			this.mainTimer = new Timer(TimerCallback, null, 0, Timeout.Infinite);
			ConsoleLog.LogMessage("Maintenance service started.");
		}

		public void Stop()
		{
			if (this.mainTimer != null)
			{
				this.mainTimer.Dispose();
				this.mainTimer = null;
			}
			timerEnabled = false;
			ConsoleLog.LogMessage("Maintenance service stopped.");
		}
		async private void TimerCallback(object state)
		{
			try
			{
				ConsoleLog.LogMessage("Running maintenance task.");
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
					if (seenPosts != null)
					{
						newEntries = newEntries.Where(e => !seenPosts.Contains(e.PostId)).ToList();
						alreadySeenCount = oldEntryCount - newEntries.Count;
					}

					if (originalEntryCount != newEntries.Count)
					{
						ConsoleLog.LogMessage($"Removing {expiredCount} expired and {alreadySeenCount} already seen entries from {user.UserName}");
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
			catch (Exception ex)
			{
				ConsoleLog.LogError($"Exception in maintenance task : {ex}");
			}
			finally
			{
				ConsoleLog.LogMessage("Maintenance complete.");
				if (this.timerEnabled)
				{
					mainTimer.Change(30000, Timeout.Infinite);
				}
			}
		}

		private async Task<List<int>> GetSeenPostIds(string userName)
		{
			if (string.IsNullOrWhiteSpace(userName)) return null;

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
			catch (Newtonsoft.Json.JsonSerializationException ex)
			{
				Console.WriteLine($"Messed up SeenPosts for {userName} - fixing.");
				if (ex.Message.Contains("Unexpected end"))
				{
					// For some reason seen posts get messed up, so we should fix 'em.
					using (var client = new HttpClient())
					{
						using (var content = new FormUrlEncodedContent(new Dictionary<string, string>()
							{
								{"username", Uri.EscapeUriString(userName) },
								{"client", $"latestchattyUWP{ Uri.EscapeUriString("SeenPosts") }" },
								{"data", "[]"}
							}))
						{
							await client.PostAsync($"{ConfigurationManager.AppSettings["winChattyApiBase"]}clientData/setClientData", content);
						}
					}
				}
			}
			catch (Exception ex)
			{
				ConsoleLog.LogError($"Error retrieving post ids for {userName} : {ex}");
			}
			return null;
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
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MaintenanceService() {
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
