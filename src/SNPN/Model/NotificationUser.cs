using System;

namespace SNPN.Model
{
	public class NotificationUser
	{
		public long Id { get; set; }
		public string UserName { get; set; }
		public DateTime DateAdded { get; set; }
		public long NotifyOnUserName { get; set; }
	}
}
