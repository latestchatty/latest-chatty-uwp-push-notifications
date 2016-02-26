using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications
{
	class NotificationUser
	{
		public MongoDB.Bson.ObjectId _id { get; set; }
		public string UserName { get; set; }
		public DateTime DateUpdated { get; set; }
		public int NotificationsSent { get; set; }
		public int ReplyCount { get; set; }
		public DateTime LastNotifiedTime { get; set; }
		public List<NotificationInfo> NotificationInfos { get; set; }
	}
}
