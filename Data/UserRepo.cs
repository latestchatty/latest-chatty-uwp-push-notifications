using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Shacknews_Push_Notifications.Data;
using Shacknews_Push_Notifications.Model;

namespace Shacknews_Push_Notifications
{
	public class UserRepo : DBHelper
	{
		public async Task<NotificationUser> FindUser(string userName)
		{
			using (var con = GetConnection())
			{
				return (
					 await con.QueryAsync<NotificationUser>(
						  @"SELECT * FROM User WHERE LOWER(UserName)=@userName",
						  new { userName = userName.ToLower() }
						  )
					 ).FirstOrDefault();
			}
		}

		public async Task<List<string>> GetAllUserNames()
		{
			using (var con = GetConnection())
			{
				return (await con.QueryAsync<string>(@"SELECT UserName FROM User")).ToList();
			}
		}



		public async Task<NotificationUser> AddUser(NotificationUser user)
		{
			using (var con = GetConnection())
			{
				user.Id = await con.QuerySingleAsync<long>(@"
					INSERT INTO User
					(UserName, DateAdded)
					VALUES(@UserName, @DateAdded);
					select last_insert_rowid();
					", new { user.UserName, user.DateAdded });
				return user;
			}
		}

		public async Task AddOrUpdateDevice(NotificationUser user, DeviceInfo notificationInfo)
		{
			using (var con = GetConnection())
			{
				var info = await con.QueryFirstOrDefaultAsync<DeviceInfo>(
					@"SELECT * FROM Device WHERE Id=@DeviceId AND UserId=@UserId",
					new { notificationInfo.DeviceId, UserId = user.Id });
				if (info == null)
				{
					await con.ExecuteAsync(
						@"INSERT INTO Device
						(Id, UserId, NotificationUri)
						VALUES(@DeviceId, @UserId, @NotificationUri)",
					new { notificationInfo.DeviceId, UserId = user.Id, notificationInfo.NotificationUri });
				}
				else
				{
					await con.ExecuteAsync(
						@"UPDATE Device SET NotificationUri=@NotificationUri WHERE Id=@DeviceId",
						new { notificationInfo.NotificationUri, notificationInfo.DeviceId });
				}
			}
		}

		public async Task<IEnumerable<DeviceInfo>> GetUserDeviceInfos(NotificationUser user)
		{
			using (var con = GetConnection())
			{
				return await con.QueryAsync<DeviceInfo>("SELECT * FROM Device WHERE UserId=@Id", new { user.Id });
			}
		}

		public async Task DeleteDevice(string deviceId)
		{
			using (var con = GetConnection())
			{
				await con.ExecuteAsync("DELETE FROM Device WHERE Id=@deviceId", new { deviceId });
			}
		}

		public async Task DeleteDeviceByUri(string uri)
		{
			using (var con = GetConnection())
			{
				await con.ExecuteAsync("DELETE FROM Device WHERE NotificationUri=@uri", new { uri });
			}
		}
	}
}