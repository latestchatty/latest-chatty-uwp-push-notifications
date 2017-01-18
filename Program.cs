using Autofac;
using Mono.Unix;
using Mono.Unix.Native;
using Shacknews_Push_Notifications.Common;
using Shacknews_Push_Notifications.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications
{
	class Program
	{
		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			var builder = new AppModuleBuilder();
			using (var container = builder.BuilderContainer())
			{
				var webService = new WebServiceProcessManager(container);
				var monitor = container.Resolve<Monitor>();
				var maintenanceService = container.Resolve<MaintenanceService>();

				webService.Start();
				monitor.Start();
				maintenanceService.Start();

				// check if we're running on mono
				if (Type.GetType("Mono.Runtime") != null)
				{
					// on mono, processes will usually run as daemons - this allows you to listen
					// for termination signals (ctrl+c, shutdown, etc) and finalize correctly
					UnixSignal.WaitAny(new[] {
						  new UnixSignal(Signum.SIGINT),
						  new UnixSignal(Signum.SIGTERM),
						  new UnixSignal(Signum.SIGQUIT),
						  new UnixSignal(Signum.SIGHUP)
					 });
				}
				else
				{
					ConsoleLog.LogMessage("Press a key to exit.");
					Console.ReadKey();
				}

				webService.Stop();
				monitor.Stop();
				maintenanceService.Stop();
			}
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			ConsoleLog.LogError($"Unhandled exception. {e}");
		}
	}
}
