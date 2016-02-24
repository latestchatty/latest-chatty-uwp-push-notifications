using Mono.Unix;
using Mono.Unix.Native;
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
			var webService = new NotificationServiceApplication();
			var monitor = new NotificationMonitor();

			webService.Start();
			monitor.Start();
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
				Console.WriteLine("Press a key to exit.");
				Console.ReadKey();
			}

			webService.Stop();
			monitor.Stop();
		}
	}
}
