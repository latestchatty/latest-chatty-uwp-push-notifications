using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications.Common
{
	public static class ConsoleLog
	{
		public static void LogMessage(string message)
		{
			Console.WriteLine($"{DateTime.UtcNow.ToString()} - {message}");
		}

		public static void LogError(string message)
		{
			Console.Error.WriteLine($"{DateTime.UtcNow.ToString()} - {message}");
		}
	}
}
