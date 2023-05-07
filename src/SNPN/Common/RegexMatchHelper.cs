using System;
using System.Text.RegularExpressions;

namespace SNPN.Common
{
	public class RegexMatchHelper {

        public static bool MatchWholeWord(string input, string matchText) {
            Regex r = new Regex($@"(^|\W){Regex.Escape(matchText)}(\W|$)", RegexOptions.IgnoreCase);
			return r.Match(input).Success;
		}
    }
}
