using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Data;
using System.Runtime.CompilerServices;
using Serilog;

namespace SNPN.Data
{
	public class DbHelper
	{
		private readonly long CurrentVersion = 1;
		private bool _initialized;
		private readonly string DbFile;
		private readonly ILogger _logger;

		public DbHelper(ILogger logger, AppConfiguration config)
		{
			_logger = logger;

			if (string.IsNullOrWhiteSpace(config.DbLocation))
			{
				DbFile = Path.Combine(Directory.GetCurrentDirectory(), "Notifications.db");
			}
			else
			{
				DbFile = config.DbLocation;
			}

			logger.Information("DB Location {dbfile}", DbFile);
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public SqliteConnection GetConnection()
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

		private SqliteConnection GetConnectionInternal(string fileLocation, bool ignoreMissingFile = false)
		{
			if (!ignoreMissingFile && !File.Exists(fileLocation))
			{
				throw new FileNotFoundException("Database file doesn't exist", fileLocation);
			}
			return new SqliteConnection("Data Source=" + fileLocation);
		}

		private void CreateDatabase(string fileLocation)
		{
			_logger.Information("Creating database at {fileLocation}", fileLocation);
			File.Create(fileLocation).Dispose();
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

		private void UpgradeDatabase()
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
							_logger.Information("Upgrading databse to version {dbupgradeversion}", i);
							UpgradeDatabase(i, con, tx);
						}
						con.Execute($"PRAGMA user_version={CurrentVersion};", transaction: tx);
						tx.Commit();
					}
				}
			}
		}

		private void UpgradeDatabase(long dbVersion, SqliteConnection con, IDbTransaction tx)
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