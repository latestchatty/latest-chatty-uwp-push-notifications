using Microsoft.AspNetCore.Hosting;
using System;
using System.Diagnostics;
using Serilog;
using SNPN.Data;

namespace SNPN
{
	public class Program
	{
		public static void Main(string[] args)
		{
			ILogger logger = null;
			try
			{
				var host = new WebHostBuilder()
					.UseUrls("http://0.0.0.0:4000")
					//.UseContentRoot(Directory.GetCurrentDirectory())
					.UseKestrel()
					.UseStartup<Startup>()
					.Build();

				logger = host.Services.GetService(typeof(ILogger)) as ILogger;
				var monitor = host.Services.GetService(typeof(Monitor.Monitor)) as Monitor.Monitor;
				var dbHelper = host.Services.GetService(typeof(DbHelper)) as DbHelper;
				
				dbHelper.GetConnection().Dispose();

				monitor.Start();
				host.Run();

				monitor.Stop();
			}
			catch (Exception ex)
			{
				logger?.Error(ex, "Unhandled exception in app.");
				if (Debugger.IsAttached) { Console.ReadKey(); }
			}
		}
	}
}
