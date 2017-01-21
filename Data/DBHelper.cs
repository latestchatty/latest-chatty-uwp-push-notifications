using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Shacknews_Push_Notifications.Data
{
    public class DBHelper
    {
        private static readonly string DB_FILE  = Directory.GetCurrentDirectory() + "Notifications.db";
        public static SqliteConnection GetConnection()
        {

            if(!File.Exists(DB_FILE))
            {
                CreateDatabase(DB_FILE);
            }
            return GetConnectionInternal(DB_FILE);
        }

        private static SqliteConnection GetConnectionInternal(string fileLocation) {
            return new SqliteConnection("Data Source=" + fileLocation);
        }

        private static void CreateDatabase(string fileLocation)
        {
            using (var connection = GetConnectionInternal(fileLocation))
            {
                connection.Open();
                using(var tx = connection.BeginTransaction())
                {
                    connection.Execute(
                        @"
                        CREATE TABLE User
                        (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            UserName VARCHAR(100) NOT NULL,
                            DateAdded INTEGER NOT NULL DEFAULT(datetime('now')),
                            NotificationsSent INTEGER NOT NULL DEFAULT 0,
                            LastNotifiedTime INTEGER NULL
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
                        ");
                    tx.Commit();
                }                
            }
        }
    }
}