public static class StringExtensions {
	public static string TruncateWithEllipsis(this string s, int maxChars)
	{
		return s.Length > maxChars ? s.Substring(0, maxChars) + "..." : s;
	}
}