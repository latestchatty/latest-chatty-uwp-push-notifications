using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications.Common
{
	public class DatabaseService
	{
		public IMongoCollection<NotificationUser> GetCollection()
		{
			var dbClient = new MongoClient(ConfigurationManager.AppSettings["dbConnectionString"]);
			var db = dbClient.GetDatabase("notifications");

			var collection = db.GetCollection<NotificationUser>("notificationUsers");
			return collection;
		}
	}
}
