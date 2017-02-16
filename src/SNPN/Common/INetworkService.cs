using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNPN.Common
{
	public interface INetworkService
	{
		Task<int> WinChattyGetNewestEventId(CancellationToken ct);

		Task<JToken> WinChattyWaitForEvent(long latestEventId, CancellationToken ct);

		Task<XDocument> GetTileContent();

		Task<bool> ReplyToNotification(string replyText, string parentId, string userName, string password);

		Task<ResponseResult> SendNotification(QueuedNotificationItem notificationItem, string token);
		Task<string> GetNotificationToken();
	}
}
