using Autofac;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using Serilog;
using SNPN.Data;

namespace SNPN
{
	public class Program
	{
		public static void Main(string[] args)
		{
			using (var container = AppModuleBuilder.Container)
			{
				var logger = container.Resolve<ILogger>();
				try
				{
					var monitor = container.Resolve<Monitor.Monitor>();
					var config = container.Resolve<AppConfiguration>();
					//This is ghetto as fffffff but just get a connection so we can make sure the DB is upgraded beofre anyone else uses it and before anything else is running.
					using (var con = UserRepo.GetConnection()) { }

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
				catch (Exception ex)
				{
					logger.Error(ex, "Unhandled exception in app.");
				}
			}
		}
	}
}
