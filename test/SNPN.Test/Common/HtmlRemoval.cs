using System;
using Xunit;
using SNPN.Common;

namespace SNPN.Test.Common
{
	public class HtmlRemovalTest
	{
		[Fact]
		public void StripBasicHtml()
		{
			var result = HtmlRemoval.StripTagsRegexCompiled("<a />");
			Assert.Equal(string.Empty, result);
		}
		[Fact]
		public void StripComplexHtml()
		{
			var result = HtmlRemoval.StripTagsRegexCompiled("New version of <span class=\"jt_blue\">Latest Chatty 8</span><br /><br />Now with loading of more posts in the main chatty!  Right now it grabs two more pages - regardless of if they're there or not.  I'll fix it so it's aware of how many pages are there and be smarter about loading only what we need to... later.<br />Also sped some things up, and got rid of some unneeded controls.  The main chatty page should feel faster and the whole thing should work a little better in the snapped view.<br /><br />If you're running Windows 8 on any device, grab the zip and run the powershell script: <a target=\"_blank\" rel=\"nofollow\" href=\"https://skydrive.live.com/redir?resid=CB8C4049A4F7C010!2517\">https://skydrive.live.com/redir?resid=CB8C4049A4F7C010!2517</a><br /><br />Plz 2 b testing syncing if you can!");
			Assert.Equal("New version of Latest Chatty 8Now with loading of more posts in the main chatty!  Right now it grabs two more pages - regardless of if they're there or not.  I'll fix it so it's aware of how many pages are there and be smarter about loading only what we need to... later.Also sped some things up, and got rid of some unneeded controls.  The main chatty page should feel faster and the whole thing should work a little better in the snapped view.If you're running Windows 8 on any device, grab the zip and run the powershell script: https://skydrive.live.com/redir?resid=CB8C4049A4F7C010!2517Plz 2 b testing syncing if you can!",
				result);
		}
		[Fact]
		public void StripSpoiler()
		{
			var result = HtmlRemoval.StripTagsRegexCompiled("Spoilers<br /><br /><span class=\"jt_spoiler\" onclick=\"this.className = '';\">Inline spoiler<br /><br />With multiple lines.  <b>And bold tag.</b><br /><br />And another line.<br /><br /><span class=\"jt_spoiler\" onclick=\"this.className = '';\">Now nested further.</span><br /></span><br />And some unspoiled at the end.");
			Assert.Equal("Spoilers______And some unspoiled at the end.",
				result);
		}
	}
}