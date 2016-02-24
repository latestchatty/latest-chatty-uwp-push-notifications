using Nancy.Hosting.Self;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications
{
	class NotificationServiceApplication
	{
		NancyHost host;

		public void Start()
		{
			if (host == null)
			{
				var uri = "http://localhost:8080"; //TODO: Read from configuration.
				this.host = new NancyHost(new Uri(uri));
				this.host.Start();
				Console.WriteLine("Web service started.");
			}
		}

		public void Stop()
		{
			try
			{
				if (host != null)
				{
					this.host.Dispose(); //Stops the host as well.
					Console.WriteLine("Web service stopped.");
				}
			}
			finally
			{
				this.host = null;
			}
		}
	}
}
