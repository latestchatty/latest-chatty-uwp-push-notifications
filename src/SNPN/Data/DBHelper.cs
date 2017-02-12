using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;
using Autofac;
using System.Data;

namespace SNPN.Data
{
	public class DbHelper
	{
		private static readonly long CURRENT_VERSION = 1;
		private static bool initialized;
		private static readonly object locker = new object();
		private static string dbFile;
		private static string DbFile
		{
			get
			{
				if (dbFile == null)
				{
					var config = AppModuleBuilder.Container.Resolve<AppConfiguration>();
					if (string.IsNullOrWhiteSpace(config.DbLocation))
					{
						dbFile = Path.Combine(Directory.GetCurrentDirectory(), "Notifications.db");
					}
					else
					{
						dbFile = config.DbLocation;
					}
				}
				return dbFile;
			}
		}

		public static SqliteConnection GetConnection()
		{
			lock (locker)
			{
				if (!File.Exists(DbFile))
				{
					CreateDatabase(DbFile);
				}
				if (!initialized)
				{
					UpgradeDatabase();
					initialized = true;
				}
				return GetConnectionInternal(DbFile);
			}
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
							NotificationsSent INTEGER NOT NULL DEFAULT 0,
							NotifyOnUserName INTEGER DEFAULT(1)
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
						PRAGMA user_version=" + CURRENT_VERSION + ";", transaction: tx);
					tx.Commit();
				}
			}
		}

		private static void UpgradeDatabase()
		{
			using (var con = GetConnectionInternal(DbFile))
			{
				con.Open();
				var dbVersion = con.QuerySingle<long>(@"PRAGMA user_version");
				if (dbVersion < CURRENT_VERSION)
				{
					using (var tx = con.BeginTransaction())
					{
						for (long i = dbVersion; i <= CURRENT_VERSION; i++)
						{
							UpgradeDatabase(i, con, tx);
						}
						con.Execute($"PRAGMA user_version={CURRENT_VERSION};", transaction: tx);
						tx.Commit();
					}
				}
			}
		}

		private static void UpgradeDatabase(long dbVersion, SqliteConnection con, IDbTransaction tx)
		{
			switch (dbVersion)
			{
				case 1:
					con.Execute(@"ALTER TABLE User ADD COLUMN NotifyOnUserName INTEGER DEFAULT(1)", transaction: tx);
					break;
			}
		}
	}
}