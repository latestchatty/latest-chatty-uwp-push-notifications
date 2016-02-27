using Autofac;
using Nancy.Hosting.Self;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications.WebService
{
	class WebServiceProcessManager
	{
		private readonly IContainer container;
		NancyHost host;

		public WebServiceProcessManager(IContainer container)
		{
			this.container = container;
		}

		public void Start()
		{
			if (host == null)
			{
				var uri = ConfigurationManager.AppSettings["hostUri"];
				var bs = new Bootstrapper(this.container);
				this.host = new NancyHost(bs, new[] { new Uri(uri) });
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
