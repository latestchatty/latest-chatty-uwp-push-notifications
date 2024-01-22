using Dapper;

namespace SNPN.Data;

public class UserRepo : DbHelper, IUserRepo
{
	private readonly ILogger<UserRepo> _logger;

	public UserRepo(ILogger<UserRepo> logger, AppConfiguration config) : base(logger, config)
	{
		_logger = logger;
	}

	public async Task<NotificationUser> FindUser(string userName)
	{
		using var con = GetConnection();
		using var multipleResult =
			 await con.QueryMultipleAsync(
				  @"
							SELECT * FROM User WHERE LOWER(UserName) = @userName;
							SELECT k.Word FROM User u 
							INNER JOIN KeywordUser ku ON ku.UserId = u.Id
							INNER JOIN Keyword k ON k.Id = ku.WordId WHERE LOWER(u.UserName) = @userName;
						",
				  new { userName = userName.ToLower() }
			 );
		var user = (await multipleResult.ReadAsync<NotificationUser>()).FirstOrDefault();
		if (user == null) return null;
		var keywords = await multipleResult.ReadAsync<string>();
		user.NotificationKeywords = keywords.ToList();
		return user;
	}

	public async Task<IEnumerable<NotificationUser>> FindUsersByWord(long wordId)
	{
		using var con = GetConnection();
		return await con.QueryAsync<NotificationUser>(
					 @"SELECT * FROM User WHERE Id IN(SELECT UserId FROM KeywordUser WHERE WordId=@wordId)",
					 new { wordId }
					 );
	}

	public async Task<IEnumerable<NotificationWord>> GetAllWordsForNotifications()
	{
		using var con = GetConnection();
		return await con.QueryAsync<NotificationWord>("SELECT * FROM Keyword");
	}

	public async Task<List<string>> GetAllUserNamesForNotification()
	{
		using var con = GetConnection();
		return (await con.QueryAsync<string>(@"SELECT UserName FROM User WHERE NotifyOnUserName=1")).ToList();
	}

	public async Task UpdateUser(NotificationUser user, bool updateKeywords)
	{
		using var con = GetConnection();
		await con.OpenAsync();
		using (var tx = con.BeginTransaction())
		{
			await con.ExecuteAsync(@"
					UPDATE User SET
						NotifyOnUserName=@NotifyOnUserName
					WHERE Id=@Id;
					", new { user.Id, user.NotifyOnUserName });
			if (updateKeywords)
			{
				await con.ExecuteAsync(@"DELETE FROM KeywordUser WHERE UserId=@Id", new { user.Id });
				if (user.NotificationKeywords != null)
				{
					foreach (var keyword in user.NotificationKeywords)
					{
						await con.ExecuteAsync(@"
							INSERT INTO Keyword (Word)
							SELECT @Word
							WHERE NOT EXISTS(SELECT 1 FROM Keyword WHERE Word = @Word);
							INSERT INTO KeywordUser (UserId, WordId)
							SELECT @Id, Id FROM Keyword WHERE Word = @Word;
						", new { Word = keyword, Id = user.Id });
					}
				}
			}
			await tx.CommitAsync();
		}
		await con.CloseAsync();
	}

	public async Task<NotificationUser> AddUser(NotificationUser user)
	{
		using var con = GetConnection();
		await con.OpenAsync();
		using (var tx = con.BeginTransaction())
		{
			user.Id = await con.QuerySingleAsync<long>(@"
					INSERT INTO User
					(UserName, DateAdded, NotifyOnUserName)
					VALUES(@UserName, @DateAdded, @NotifyOnUserName);
					select last_insert_rowid();
					", new { user.UserName, user.DateAdded, user.NotifyOnUserName });

			if (user.NotificationKeywords != null)
			{
				foreach (var keyword in user.NotificationKeywords)
				{
					await con.ExecuteAsync(@"
							INSERT INTO Keyword (Word)
							SELECT @Word
							WHERE NOT EXISTS(SELECT 1 FROM Keyword WHERE Word = @Word);
							INSERT INTO KeywordUser (UserId, WordId)
							SELECT @Id, Id FROM Keyword WHERE Word = @Word;
						", new { Word = keyword, Id = user.Id });
				}

			}
			await tx.CommitAsync();
		}
		await con.CloseAsync();
		return user;
	}

	public async Task AddOrUpdateDevice(NotificationUser user, DeviceInfo notificationInfo)
	{
		using var con = GetConnection();
		var info = await con.QueryFirstOrDefaultAsync<DeviceInfo>(
			 @"SELECT * FROM Device WHERE Id=@DeviceId AND UserId=@UserId",
			 new { notificationInfo.DeviceId, UserId = user.Id });
		if (info == null)
		{
			_logger.LogDebug("{DeviceId} doesn't exist for {UserId} - attempting to add with {NotificationUri}", notificationInfo.DeviceId, user.Id, notificationInfo.NotificationUri);
			// Delete other device ids prior to insserting. If someone logs in to a different account on the same device they should get notifications for their current user.
			await con.ExecuteAsync(
				 @"DELETE FROM Device WHERE Id=@DeviceId;
						INSERT INTO Device
						(Id, UserId, NotificationUri)
						VALUES(@DeviceId, @UserId, @NotificationUri);",
			new { notificationInfo.DeviceId, UserId = user.Id, notificationInfo.NotificationUri });
		}
		else
		{
			_logger.LogDebug("{DeviceId} exists for {UserId} - attempting to update with {NotificationUri}", notificationInfo.DeviceId, user.Id, notificationInfo.NotificationUri);
			await con.ExecuteAsync(
				 @"UPDATE Device SET NotificationUri=@NotificationUri WHERE Id=@DeviceId",
				 new { notificationInfo.NotificationUri, notificationInfo.DeviceId });
		}
	}

	public async Task<List<DeviceInfo>> GetUserDeviceInfos(NotificationUser user)
	{
		using var con = GetConnection();
		return (await con.QueryAsync<DeviceInfo>("SELECT * FROM Device WHERE UserId=@Id", new { user.Id })).ToList();
	}

	public async Task DeleteDevice(string deviceId)
	{
		using var con = GetConnection();
		await con.ExecuteAsync("DELETE FROM Device WHERE Id=@deviceId", new { deviceId });
	}

	public async Task DeleteDeviceByUri(string uri)
	{
		using var con = GetConnection();
		await con.ExecuteAsync("DELETE FROM Device WHERE NotificationUri=@uri", new { uri });
	}
}