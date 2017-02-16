using Microsoft.Extensions.Caching.Memory;
using Serilog;
using SNPN.Common;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNPN.WebService
{
	public class TileContentRepo
	{
		private readonly INetworkService networkService;
		private readonly MemoryCache cache;
		private readonly ILogger logger;

		public TileContentRepo(INetworkService networkService, ILogger logger, MemoryCache cache)
		{
			this.networkService = networkService;
			this.logger = logger;
			this.cache = cache;
		}

		public async Task<string> GetTileContent()
		{
			var tileContent = this.cache.Get("tileContent") as string;
			if (string.IsNullOrWhiteSpace(tileContent))
			{
				this.logger.Information("Retrieving tile content.");

				var xDoc = await this.networkService.GetTileContent();

				var items = xDoc.Descendants("item");
				var itemsObj = items.Select(i => new
				{
					Title = i.Element("title").Value,
					PublishDate = DateTime.Parse(i.Element("pubDate").Value.Replace("PDT", "").Replace("PST", "").Trim()),
					Author = i.Element("author").Value
				}).OrderByDescending(i => i.PublishDate).Take(3).ToList();

				var item = itemsObj.FirstOrDefault();

				if (item == null) return string.Empty;

				var visualElement = new XElement("visual", new XAttribute("version", "2"));
				var tileElement = new XElement("tile", visualElement);

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileWide310x150Text09"), new XAttribute("fallback", "TileWideText09"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title)));

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileSquare150x150Text02"), new XAttribute("fallback", "TileSquareText02"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title)));

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileSquare310x310TextList03"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title),
					new XElement("text", new XAttribute("id", "3"), $"{ itemsObj.ElementAt(1).Author} posted"),
					new XElement("text", new XAttribute("id", "4"), itemsObj.ElementAt(1).Title),
					new XElement("text", new XAttribute("id", "5"), $"{itemsObj.ElementAt(2).Author} posted"),
					new XElement("text", new XAttribute("id", "6"), itemsObj.ElementAt(2).Title)));
				var doc = new XDocument(tileElement);
				tileContent = doc.ToString(SaveOptions.DisableFormatting);
				this.cache.Set("tileContent", tileContent, DateTimeOffset.UtcNow.AddMinutes(5));
			}
			else
			{
				this.logger.Information("Retrieved cached tile content.");
			}
			return tileContent;
		}
	}
}
