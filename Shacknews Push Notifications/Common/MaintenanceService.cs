using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications.Common
{
	class MaintenanceService
	{
		DatabaseService dbService;
		NotificationService notificationService;
		Timer mainTimer;
		bool timerEnabled = false;

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
				Console.WriteLine("Running maintenance task.");
				var collection = dbService.GetCollection();
				var usersThatNeedCleanup = await collection.Find(u => u.ReplyEntries.Any(re => re.Expiration < DateTime.UtcNow)).ToListAsync();
				foreach (var user in usersThatNeedCleanup)
				{
					var oldEntryCount = user.ReplyEntries.Count;
					var newEntries = user.ReplyEntries.Where(e => e.Expiration > DateTime.UtcNow).ToList();
					Console.WriteLine($"Removing {oldEntryCount - newEntries.Count} entries from {user.UserName}");
					var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
					var update = Builders<NotificationUser>.Update
						.Set(x => x.ReplyEntries, newEntries)
						.CurrentDate(x => x.DateUpdated);
					await collection.UpdateOneAsync(filter, update);

					var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", newEntries.Count)));
					await this.notificationService.QueueNotificationToUser(NotificationType.Badge, badgeDoc, user.UserName);
				}
			}
			finally
			{
				Console.WriteLine("Maintenance complete.");
				if (this.timerEnabled)
				{
					mainTimer = new Timer(TimerCallback, null, 10000, Timeout.Infinite);
				}
			}
		}
	}
}
