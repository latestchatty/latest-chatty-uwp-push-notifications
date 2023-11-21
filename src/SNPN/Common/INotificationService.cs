namespace SNPN.Common;

public interface INotificationService
{
	void QueueNotificationData(NotificationType type, string notificationUri, Post post, NotificationMatchType matchType, string title, string message, NotificationGroups group = NotificationGroups.None, int ttl = 0);
}
