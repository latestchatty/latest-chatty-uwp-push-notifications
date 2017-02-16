using Microsoft.Extensions.Caching.Memory;
using Moq;
using SNPN.Common;
using SNPN.WebService;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace SNPN.Test.WebService
{
	public class TileContentRepoTests
	{
		private readonly string RssContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<rss version=""2.0"" xmlns:atom=""http://www.w3.org/2005/Atom"" xmlns:steam=""http://www.shacknews.com/steamfeed.xml"">
	 <channel>
	 <title>Shacknews Recent Articles</title>
	 <atom:link href=""http://www.shacknews.com/rss?recent_articles=1"" rel=""self"" type=""application/rss+xml"" />
	 <link>http://www.shacknews.com/rss?recent_articles=1</link>
	 <description></description>
	 <language>en-us</language>
		  <item>
				<title>Injustice 2 Mobile Gameplay Video Leaks Cyborg and Scarecrow </title>
				<link>http://www.shacknews.com/article/99065/injustice-2-mobile-gameplay-video-leaks-cyborg-and-scarecrow</link>
				<guid isPermaLink=""true"">http://www.shacknews.com/article/99065/injustice-2-mobile-gameplay-video-leaks-cyborg-and-scarecrow</guid>
				<description><![CDATA[<p>A mobile version of Injustice 2 has soft-launched in the Philippines and gameplay footage may have inadvertently leaked two as-yet unannounced heroes that could appear in the console version when it launches in May.</p>
<p>The video, from <a href=""https://www.youtube.com/watch?v=Npv3OIV8bo8"">mobile site All-Star Production</a>&nbsp;(via <a href=""https://www.eventhubs.com/news/2017/feb/14/scarecrow-cyborg-and-green-lantern-headed-injustice-2-first-footage-mobile-game-features-all-three-alongside-current-console-characters/"">EventHubs</a>), shows off Cyborg, Green Lantern and Scarecrow among the combatants for iOS and Android. Green Lantern had previously been confirmed as playable through an alternate skin, but the other two have not been announced by either developer NetherRealm or publisher Warner Bros. Interactive. The latest reveal&nbsp;earlier this week&nbsp;was of <a href=""http://www.shacknews.com/article/99031/injustice-2-girls-trailer-introduces-cheetah-catwoman"" target=""_blank"">Cheetah, Catwoman and Poison Ivy</a>, and the <a href=""https://www.injustice.com/"" target=""_blank"">official Injustice website has the next hero(es)</a>&nbsp;set to be announced&nbsp;on February 23.&nbsp;</p>
<p>It is possible that the two could be exclusive to the app, as the free-to-play Injustice: Gods Among Us released in April 2013 alongside the console versions of the game had a few heroes of its own.</p>
<p>The console game is scheduled to be released on May 16 for PlayStation 4 and Xbox One. As for the mobile title, there is no official release date for it in the United States.&nbsp;</p>
<p><iframe width=""700"" height=""394"" src=""https://www.youtube.com/embed/Npv3OIV8bo8"" frameborder=""0"" allowfullscreen=""allowfullscreen""></iframe></p>
<p></p>]]></description>
				<pubDate>Wed, 15 Feb 2017 18:10:00 PST</pubDate>
				<author>John Keefer</author>
		  </item>
		  <item>
				<title>PlayStation Now to Discontinue Service to PS3, Vita, and Smart TVs</title>
				<link>http://www.shacknews.com/article/99064/playstation-now-to-discontinue-service-to-ps3-vita-and-smart-tvs</link>
				<guid isPermaLink=""true"">http://www.shacknews.com/article/99064/playstation-now-to-discontinue-service-to-ps3-vita-and-smart-tvs</guid>
				<description><![CDATA[<p>If you have a subsciption to PlayStation Now and use it for your PS3, PS Vita or PlayStation TV, get ready to cancel your payment. Sony has revealed that it will no longer be offering the streaming service on those platforms starting August 15.</p>
<p><span>""After thoughtful consideration, we decided to shift our focus and resources to PS4 and Windows PC to further develop and improve the user experience on these two devices,"" Brian Dunn, senior marketing manager for PlayStation Now, <a href=""http://blog.us.playstation.com/2017/02/15/playstation-now-service-update/"">said in a blog post</a>. ""This move puts us in the best position to grow the service even further.""</span></p>
<p><span>The list of devices that will no longer support PlayStation Now include:</span></p>
<ul>
<li>PlayStation 3</li>
<li>PlayStation Vita and PlayStation TV</li>
<li>All 2013, 2014, 2015 Sony Bravia TV models</li>
<li>All Sony Blu-ray player models</li>
<li>All Samsung TV models</li>
</ul>
<p>Sony Bravia TVs from 2016 will be discontinued as of April 1.</p>
<p>If you have autopay setup, be sure to check when devices you have and disable the payments&nbsp;before your next payment comes due so you are not charged for something you won;t be able to use.</p>
<p>The move is interesting given that it has decided to forego streaming on its own devices in order to continue to pursue a PC audience. Sony just <a href=""http://www.shacknews.com/article/96571/playstation-now-pc-streaming-available-now"">began offering the service to PC users</a>&nbsp;in late August, Currently the service offers more than 450 PS3 games.</p>]]></description>
				<pubDate>Wed, 15 Feb 2017 17:25:00 PST</pubDate>
				<author>John Keefer</author>
		  </item>
		  <item>
				<title>Zelda&#039;s Eiji Aonuma Has a &#039;Trick&#039; to Help Open World Narratives</title>
				<link>http://www.shacknews.com/article/99063/zeldas-eiji-aonuma-has-a-trick-to-help-open-world-narratives</link>
				<guid isPermaLink=""true"">http://www.shacknews.com/article/99063/zeldas-eiji-aonuma-has-a-trick-to-help-open-world-narratives</guid>
				<description><![CDATA[<p>Open world games can be somewhat challenging for cohesive storytelling. With The Legend of Zelda: Breath of the Wild changing to an open world format, Zelda producer&nbsp;<span>Eiji Aonuma said that he was aware of the narrative issues, but has something up his sleeve to deal with the issue.</span></p>
<p><span><span>""He knows the secret of how to do it,"" Shigeru Miyamoto said in a&nbsp;</span><a href=""http://www.gameinformer.com/b/features/archive/2017/02/15/miyamoto-and-aonuma-on-zeldas-storytelling-and-breath-of-the-wilds-trick.aspx"">Game Informer video interview</a>, referencing Aonuma sitting next to him.&nbsp;Aonuma agreed, saying&nbsp;<span>""There is a little bit of a trick that I implemented this time. This idea is something I've had since I started developing games 20-some odd years ago. So I really want you to look forward to playing the game and finding this something that I put in there.""</span></span></p>
<p><span>Aonuma even hinted that some of the trailers that had been released included story snippets that fans may have overlooked. Unfortunately,&nbsp;neither wanted to go into any more detail, but were happy to wax philosophically about narrative driven games.&nbsp;</span></p>
<p><span><span>""When you're playing a game, the story is there to give the big world you're in some substance and meat,"" Miyamoto said. ""And because you're the protagonist in the game, that's what you should be doing. I think, also, when a story is set too strictly already, you can only follow a certain path. There's also times where it takes so much time to set up the story, that you just want to get into the gameplay, but you can't because there's so much setup.""</span></span></p>
<p><span><span>Of course, Miyamoto quickly clarified that this wasn't the case with Breath of the Wild, to which Aonuma readily agreed. But he added that in games like Ocarina of Time, where players had to be introduced to four girls, storytelling was an important part of the way the game progressed.</span></span></p>
<p>The Legend of Zelda: Breath of the Wild is set to launch on March 3 alongside the Nintendo Switch. the game is also coming out for Wii U.</p>
<p></p>]]></description>
				<pubDate>Wed, 15 Feb 2017 16:55:00 PST</pubDate>
				<author>John Keefer</author>
		  </item>
		  <item>
				<title>Shack&#039;s Arcade Corner: Tapper</title>
				<link>http://www.shacknews.com/article/99061/shacks-arcade-corner-tapper</link>
				<guid isPermaLink=""true"">http://www.shacknews.com/article/99061/shacks-arcade-corner-tapper</guid>
				<description><![CDATA[<p>We take a look back at Tapper, the classic beered-up arcade game, on this week's episode of Shack's Arcade Corner. The game involved serving patrons at a bar with nice frosty mugs of beer. This later became Root Beer in the more family-friendly version of the game. When the customers finished their beers, the mugs came sliding back towards the bartender. The delicate balance of serving drinks and grabbing empty mugs before they hit the floor creates a fast pace game that is still fun to this very day.</p>
<p>The arcade cabinet came in several styles and allowed for left or right handed players to step up to the bar. Players controlled the bartender with a traditional joystick but had to pull down on a tap-like handle to pour beers. This was also one of the first video games to see an advertising sponsor as Budweiser logos were prominently placed throughout the game. There was also a mini&nbsp;game in between the 4 different stages where players had to select a beer that had not been shaken. This is truly a game that is as fun today as it was 33 years ago. Let's all crack open a cold one and take an even deeper look at a&nbsp;legendary&nbsp;arcade game, Tapper.</p>
<p><span>For more&nbsp;videos, including gameplay and interviews, visit the&nbsp;</span><a href=""https://www.youtube.com/user/Shacknewsgames"" target=""_blank"">Shacknews</a><span>&nbsp;and&nbsp;</span><a href=""https://www.youtube.com/channel/UCWdXbx28xPxbotxGIQDjIbA"" target=""_blank"">GamerHub.tv</a><span>&nbsp;YouTube channels.</span></p>
<p><iframe width=""700"" height=""369"" src=""https://www.youtube.com/embed/J07DZSmaWSY?ecver=1"" frameborder=""0"" allowfullscreen=""allowfullscreen""></iframe></p>
<p><span>If you have a suggestion for a future episode of Shack's Arcade Corner, please let us know in the comments section or tweet&nbsp;</span><a href=""https://twitter.com/shacknews"" target=""_blank"">@shacknews</a><span>&nbsp;&amp;&nbsp;</span><a href=""https://twitter.com/GregBurke85"" target=""_blank"">@GregBurke85</a><span>&nbsp;with #ArcadeCorner.</span></p>]]></description>
				<pubDate>Wed, 15 Feb 2017 16:20:00 PST</pubDate>
				<author>Greg Burke</author>
		  </item>
	 </channel>
</rss>
";
		private readonly string ExpectedResult = "<tile><visual version=\"2\"><binding template=\"TileWide310x150Text09\" fallback=\"TileWideText09\"><text id=\"1\">John Keefer posted</text><text id=\"2\">Injustice 2 Mobile Gameplay Video Leaks Cyborg and Scarecrow </text></binding><binding template=\"TileSquare150x150Text02\" fallback=\"TileSquareText02\"><text id=\"1\">John Keefer posted</text><text id=\"2\">Injustice 2 Mobile Gameplay Video Leaks Cyborg and Scarecrow </text></binding><binding template=\"TileSquare310x310TextList03\"><text id=\"1\">John Keefer posted</text><text id=\"2\">Injustice 2 Mobile Gameplay Video Leaks Cyborg and Scarecrow </text><text id=\"3\">John Keefer posted</text><text id=\"4\">PlayStation Now to Discontinue Service to PS3, Vita, and Smart TVs</text><text id=\"5\">John Keefer posted</text><text id=\"6\">Zelda's Eiji Aonuma Has a 'Trick' to Help Open World Narratives</text></binding></visual></tile>";

		[Fact]
		async void TileContent()
		{
			var logger = new Mock<Serilog.ILogger>();
			var networkService = new Mock<INetworkService>();
			networkService.Setup(ns => ns.GetTileContent())
				.Returns(Task.FromResult(XDocument.Parse(RssContent)));

			var memCache = new MemoryCache(new MemoryCacheOptions());
			var repo = new TileContentRepo(networkService.Object, logger.Object, memCache);
			var result = await repo.GetTileContent();
			Assert.Equal(ExpectedResult, result);
		}

		[Fact]
		async void CacheHit()
		{
			var logger = new Mock<Serilog.ILogger>();
			var networkServiceCallCount = 0;
			var networkService = new Mock<INetworkService>();
			networkService.Setup(ns => ns.GetTileContent())
				.Callback(() => networkServiceCallCount++)
				.Returns(Task.FromResult(XDocument.Parse(RssContent)));

			var memCache = new MemoryCache(new MemoryCacheOptions());
			var repo = new TileContentRepo(networkService.Object, logger.Object, memCache);
			await repo.GetTileContent();
			await repo.GetTileContent();
			await repo.GetTileContent();

			Assert.Equal(1, networkServiceCallCount);
		}
	}
}
