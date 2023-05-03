using System.Xml.Linq;
using SNPN.Model;

namespace SNPN.Common
{
	public interface INotificationService
	{
		void QueueNotificationData(NotificationType type, DeviceInfo deviceInfo, Post post, string title, string message, NotificationGroups group = NotificationGroups.None, int ttl = 0);
	}
}
