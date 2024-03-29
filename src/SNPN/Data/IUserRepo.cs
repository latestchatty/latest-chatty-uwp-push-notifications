﻿namespace SNPN.Data;

public interface IUserRepo
{
	Task AddOrUpdateDevice(NotificationUser user, DeviceInfo notificationInfo);
	Task<NotificationUser> AddUser(NotificationUser user);
	Task DeleteDevice(string deviceId);
	Task DeleteDeviceByUri(string uri);
	Task<NotificationUser> FindUser(string userName);
	Task<List<string>> GetAllUserNamesForNotification();
	Task<List<DeviceInfo>> GetUserDeviceInfos(NotificationUser user);
	Task UpdateUser(NotificationUser user, bool updateKeywords);
	Task<IEnumerable<NotificationUser>> FindUsersByWord(long wordId);
	Task<IEnumerable<NotificationWord>> GetAllWordsForNotifications();
}