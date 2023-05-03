using System.Xml.Linq;
using SNPN.Model;

namespace SNPN.Common
{
	public class QueuedNotificationItem
	{
		public QueuedNotificationItem(NotificationType type, XDocument content, Post post, string uri = null, NotificationGroups group = NotificationGroups.None, string tag = null, int ttl = 0, string title = null, string message = null)
		{
			Type = type;
			Content = content;
			Uri = uri;
			Group = group;
			Tag = tag;
			Title = title;
			Message = message;
			Ttl = ttl;
			Post = post;
		}

		public XDocument Content { get; private set; }
		public NotificationType Type { get; private set; }
		public string Uri { get; private set; }
		public NotificationGroups Group { get; private set; }
		public string Tag { get; private set; }
		public string Title { get; private set; }
		public string Message { get; private set; }
		public int Ttl { get; private set; }
		public Post Post { get; private set; }
	}
}
