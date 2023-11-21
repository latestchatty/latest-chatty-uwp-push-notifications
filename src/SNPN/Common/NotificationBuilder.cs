using System.Xml.Linq;

namespace SNPN.Common;

public static class NotificationBuilder
{
	public static XDocument BuildReplyDoc(int postId, string title, string message)
	{
		var toastDoc = new XDocument(
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
								  new XAttribute("arguments", $"reply={postId}")/*,
											new XAttribute("imageUri", "Assets/success.png"),
											new XAttribute("hint-inputId", "message")*/)
				  )
			 )
		);

		return toastDoc;
	}
}
