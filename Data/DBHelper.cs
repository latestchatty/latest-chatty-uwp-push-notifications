using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;
using Autofac;
using Shacknews_Push_Notifications.Common;

namespace Shacknews_Push_Notifications.Data
{
	public class DBHelper
	{
		private static string db_file;
		private static string DB_FILE
		{
			get
			{
				if (db_file == null)
				{
					var config = AppModuleBuilder.Container.Resolve<AppConfiguration>();
					if (string.IsNullOrWhiteSpace(config.DBLocation))
					{
						db_file = Path.Combine(Directory.GetCurrentDirectory(), "Notifications.db");
					}
					else
					{
						db_file = config.DBLocation;
					}
				}
				return db_file;
			}
		}

		public static SqliteConnection GetConnection()
		{

			if (!File.Exists(DB_FILE))
			{
				CreateDatabase(DB_FILE);
			}
			return GetConnectionInternal(DB_FILE);
		}

		private static SqliteConnection GetConnectionInternal(string fileLocation)
		{
			return new SqliteConnection("Data Source=" + fileLocation);
		}

		private static void CreateDatabase(string fileLocation)
		{
			using (var connection = GetConnectionInternal(fileLocation))
			{
				connection.Open();
				using (var tx = connection.BeginTransaction())
				{
					connection.Execute(
						@"
						CREATE TABLE User
						(
							Id INTEGER PRIMARY KEY AUTOINCREMENT,
							UserName VARCHAR(100) NOT NULL,
							DateAdded INTEGER NOT NULL DEFAULT(datetime('now')),
							NotificationsSent INTEGER NOT NULL DEFAULT 0
						);
						CREATE INDEX UserUserName ON User(UserName);
						CREATE TABLE Device
						(
							Id VARCHAR(300) UNIQUE NOT NULL,
							UserId INTEGER NOT NULL,
							NotificationUri VARCHAR(1000) NOT NULL,
							FOREIGN KEY(UserId) REFERENCES User(Id)
						);
						CREATE INDEX DeviceUserId ON Device(UserId);
						CREATE INDEX DeviceId ON Device(Id);
						CREATE INDEX DeviceNotificationUri ON Device(NotificationUri);
						");
					tx.Commit();
				}
			}
		}
	}
}