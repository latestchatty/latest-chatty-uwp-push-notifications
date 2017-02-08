using System.Xml.Linq;

namespace SNPN.Common
{
	public interface INotificationService
	{
		void QueueNotificationData(NotificationType type, string notificationUri, XDocument content = null, NotificationGroups group = NotificationGroups.None, string tag = null, int ttl = 0);
	}
}
