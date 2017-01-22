using System;

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
