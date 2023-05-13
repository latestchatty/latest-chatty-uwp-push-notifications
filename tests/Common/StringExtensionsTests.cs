using Xunit;

public class StringExtensionsTests
{
	[Theory]
	[InlineData("Hello World", 5, "Hello...")]
	[InlineData("Hello World", 11, "Hello World")]
	[InlineData("Hello World", 25, "Hello World")]
	public void Truncate(string input, int maxChars, string expected)
	{
		Assert.Equal(input.TruncateWithEllipsis(maxChars), expected);
	}
}