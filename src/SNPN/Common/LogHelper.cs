using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SNPN.Common
{
	public class LogHelper {

        private static int MAX_ABBREV_STRING_LENGTH = 30;
        public static string GetAbbreviatedString(string id) {
			return id?[0..Math.Min(id.Length, MAX_ABBREV_STRING_LENGTH)];
		}
    }
}
