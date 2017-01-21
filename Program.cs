using Autofac;
using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Shacknews_Push_Notifications.Common;

namespace Shacknews_Push_Notifications
{
	public class Program
	{
		public static void Main(string[] args)
		{
			using (var container = AppModuleBuilder.Container)
			{
				var monitor = container.Resolve<Monitor>();

				monitor.Start();

				Console.WriteLine("Hello World!");
				var host = new WebHostBuilder()
					.UseUrls("http://0.0.0.0:4000")
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
