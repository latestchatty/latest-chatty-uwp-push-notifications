using System;
using System.Collections.Generic;

namespace Shacknews_Push_Notifications.Model
{
	public class NotificationUser
	{
		public int Id { get; set; }
		public string UserName { get; set; }
		public DateTime DateUpdated { get; set; }
		public int NotificationsSent { get; set; }
		public DateTime LastNotifiedTime { get; set; }
		public List<NotificationInfo> NotificationInfos { get; set; }
	}
}
