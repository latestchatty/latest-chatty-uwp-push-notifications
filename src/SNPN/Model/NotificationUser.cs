using System;
using System.Collections.Generic;

namespace Shacknews_Push_Notifications.Model
{
	public class NotificationUser
	{
		public long Id { get; set; }
		public string UserName { get; set; }
		public DateTime DateAdded { get; set; }
		public long NotifyOnUserName { get; set; }
	}
}
