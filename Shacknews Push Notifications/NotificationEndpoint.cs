using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications
{
	public class NotificationEndpoint : NancyModule
	{
		public NotificationEndpoint()
		{
			Get["/test"] = x => "This is a test.";
		}
	}
}
