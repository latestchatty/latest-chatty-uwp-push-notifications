using Autofac;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;

namespace Shacknews_Push_Notifications
{
	public class Program
	{
		public static void Main(string[] args)
		{
			using (var container = AppModuleBuilder.Container)
			{
				var monitor = container.Resolve<Monitor>();
				var config = container.Resolve<AppConfiguration>();
				monitor.Start();

				var host = new WebHostBuilder()
					.UseUrls(config.HostUrl)
					.UseContentRoot(Directory.GetCurrentDirectory())
					.UseKestrel()
					.UseStartup<Startup>()
					.Build();

				host.Run();

				monitor.Stop();
			}
		}
	}
}
