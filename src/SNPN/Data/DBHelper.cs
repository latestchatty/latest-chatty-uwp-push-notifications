using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Runtime.CompilerServices;

namespace SNPN.Data;

public class DbHelper
{
	private readonly long CurrentVersion = 2;
	private bool _initialized;
	private readonly string DbFile;
	private readonly ILogger<DbHelper> _logger;

	public DbHelper(ILogger<DbHelper> logger, AppConfiguration config)
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

		logger.LogInformation("DB Location {dbfile}", DbFile);
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
		_logger.LogInformation("Creating database at {fileLocation}", fileLocation);
		File.Create(fileLocation).Dispose();
		using var connection = GetConnectionInternal(fileLocation, true);
		connection.Open();
		using var tx = connection.BeginTransaction();
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

						CREATE TABLE Keyword
						(
							Id INTEGER PRIMARY KEY AUTOINCREMENT,
							Word VARCHAR(500) UNIQUE NOT NULL
						);
						CREATE INDEX KeywordWord ON Keyword(Word);
						
						CREATE TABLE KeywordUser
						(
							UserId INTEGER NOT NULL,
							WordId INTEGER NOT NULL,
							FOREIGN KEY(UserId) REFERENCES User(Id),
							FOREIGN KEY(WordId) REFERENCES Keyword(Id)
						);
						CREATE INDEX KeywordUserUserId ON KeywordUser(UserId);
						CREATE INDEX KeywordUserWordId ON KeywordUser(WordId);

						PRAGMA user_version=" + CurrentVersion + ";", transaction: tx);
		tx.Commit();
	}

	private void UpgradeDatabase()
	{
		using var con = GetConnectionInternal(DbFile);
		con.Open();
		var dbVersion = con.QuerySingle<long>(@"PRAGMA user_version");
		if (dbVersion < CurrentVersion)
		{
			using var tx = con.BeginTransaction();
			for (long i = dbVersion + 1; i <= CurrentVersion; i++)
			{
				_logger.LogInformation("Upgrading databse to version {dbupgradeversion}", i);
				UpgradeDatabase(i, con, tx);
			}
			con.Execute($"PRAGMA user_version={CurrentVersion};", transaction: tx);
			tx.Commit();
		}
	}

	private void UpgradeDatabase(long dbVersion, SqliteConnection con, IDbTransaction tx)
	{
		switch (dbVersion)
		{
			case 1:
				con.Execute(@"ALTER TABLE User ADD COLUMN NotifyOnUserName INTEGER DEFAULT(1)", transaction: tx);
				break;
			case 2:
				con.Execute(@"
						CREATE TABLE Keyword
						(
							Id INTEGER PRIMARY KEY AUTOINCREMENT,
							Word VARCHAR(500) UNIQUE NOT NULL
						);
						CREATE INDEX KeywordWord ON Keyword(Word);
						
						CREATE TABLE KeywordUser
						(
							UserId INTEGER NOT NULL,
							WordId INTEGER NOT NULL,
							FOREIGN KEY(UserId) REFERENCES User(Id),
							FOREIGN KEY(WordId) REFERENCES Keyword(Id)
						);
						CREATE INDEX KeywordUserUserId ON KeywordUser(UserId);
						CREATE INDEX KeywordUserWordId ON KeywordUser(WordId);
						");
				break;
		}
	}
}