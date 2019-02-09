using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;
using Autofac;
using System.Data;
using System.Runtime.CompilerServices;

namespace SNPN.Data
{
	public class DbHelper
	{
		private static readonly long CurrentVersion = 1;
		private static bool _initialized;
		private static string _dbFile;
		private static string DbFile
		{
			get
			{
				if (_dbFile == null)
				{
					var config = AppModuleBuilder.Container.Resolve<AppConfiguration>();
					if (string.IsNullOrWhiteSpace(config.DbLocation))
					{
						_dbFile = Path.Combine(Directory.GetCurrentDirectory(), "Notifications.db");
					}
					else
					{
						_dbFile = config.DbLocation;
					}
				}
				return _dbFile;
			}
		}

        [MethodImpl(MethodImplOptions.Synchronized)]
		public static SqliteConnection GetConnection()
		{
			if (!File.Exists(DbFile))
			{
				CreateDatabase(DbFile);
			}
			if (!_initialized)
			{
				UpgradeDatabase();
				_initialized = true;
			}
			return GetConnectionInternal(DbFile);
		}

		private static SqliteConnection GetConnectionInternal(string fileLocation, bool ignoreMissingFile = false)
		{
			if (!ignoreMissingFile && !File.Exists(fileLocation))
			{
				throw new FileNotFoundException("Database file doesn't exist", fileLocation);
			}
			return new SqliteConnection("Data Source=" + fileLocation);
		}

		private static void CreateDatabase(string fileLocation)
		{
			using (var connection = GetConnectionInternal(fileLocation, true))
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
						PRAGMA user_version=" + CurrentVersion + ";", transaction: tx);
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
				if (dbVersion < CurrentVersion)
				{
					using (var tx = con.BeginTransaction())
					{
						for (long i = dbVersion; i <= CurrentVersion; i++)
						{
							UpgradeDatabase(i, con, tx);
						}
						con.Execute($"PRAGMA user_version={CurrentVersion};", transaction: tx);
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