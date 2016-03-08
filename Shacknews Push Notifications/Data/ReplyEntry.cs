using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications.Data
{
	public class ReplyEntry
	{
		public DateTime Expiration { get; set; }
		public int PostId { get; set; }

		public ReplyEntry(DateTime expiration, int postId)
		{
			this.Expiration = expiration;
			this.PostId = postId;
		}
	}
}
