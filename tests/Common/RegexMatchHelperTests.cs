using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Net;
using System.Collections.Generic;
using SNPN.Common;

namespace SNPN.Test.Common
{
	public class RegexMatchHelperTests
	{
		[Fact]
		void RegexMatchOnWordBoundaries()
		{
            Assert.True(RegexMatchHelper.MatchWholeWord("Hello there test.", "TEST"));
            Assert.True(RegexMatchHelper.MatchWholeWord("Hello there,test how are you", "TEST"));
            Assert.True(RegexMatchHelper.MatchWholeWord("Hello there,test,how are you", "test"));
            Assert.True(RegexMatchHelper.MatchWholeWord("Test how are you", "test"));
            Assert.True(RegexMatchHelper.MatchWholeWord(@"How are you
                sure do hope you like multiline test
                yep", "test"));
            Assert.True(RegexMatchHelper.MatchWholeWord("Test, how are you", "test"));
            Assert.True(RegexMatchHelper.MatchWholeWord("-Test-!, how are you", "-test-"));
            Assert.True(RegexMatchHelper.MatchWholeWord("Hello there test ", "test"));
            Assert.True(RegexMatchHelper.MatchWholeWord("Will this annoy node", "node"));
            Assert.True(RegexMatchHelper.MatchWholeWord("Will this annoy node?", "node"));

            Assert.False(RegexMatchHelper.MatchWholeWord("Hello there testtest.", "test"));
            Assert.False(RegexMatchHelper.MatchWholeWord("Should not match node-js?", "node"));
            Assert.False(RegexMatchHelper.MatchWholeWord("Hello there", "test"));
        }
    }
}
