using SNPN.Common;
using System.Xml.Linq;
using Xunit;

namespace SNPN.Test.Common
{
	public class NotificationBuilderTests
	{
		[Fact]
		void ReplyNotification()
		{
			var postId = 1;
			var title = "Test Title";
			var message = "Message Body";

			var expectedDoc = new XDocument(
				 new XElement("toast", new XAttribute("launch", $"goToPost?postId={postId}"),
					  new XElement("visual",
							new XElement("binding", new XAttribute("template", "ToastText02"),
								 new XElement("text", new XAttribute("id", "1"), title),
								 new XElement("text", new XAttribute("id", "2"), message)
							)
					  ),
					  new XElement("actions",
								 new XElement("input", new XAttribute("id", "message"),
									  new XAttribute("type", "text"),
									  new XAttribute("placeHolderContent", "reply")),
								 new XElement("action", new XAttribute("activationType", "background"),
									  new XAttribute("content", "reply"),
									  new XAttribute("arguments", $"reply={postId}")
									  )
					  )
				 )
			);
			var xDoc = NotificationBuilder.BuildReplyDoc(postId, title, message);

			Assert.Equal(expectedDoc.ToString(), xDoc.ToString());
		}
	}
}
