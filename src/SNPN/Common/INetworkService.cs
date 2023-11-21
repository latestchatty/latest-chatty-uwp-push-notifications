using System.Text.Json;
using System.Xml.Linq;

namespace SNPN.Common;

public interface INetworkService
{
	Task<int> WinChattyGetNewestEventId(CancellationToken ct);

	Task<JsonElement> WinChattyWaitForEvent(long latestEventId, CancellationToken ct);

	Task<XDocument> GetTileContent();

	Task<bool> ReplyToNotification(string replyText, string parentId, string userName, string password);

	Task<ResponseResult> SendNotificationWNS(QueuedNotificationItem notificationItem, string token);
	Task<ResponseResult> SendNotificationFCM(QueuedNotificationItem notificationItem);
	Task<string> GetNotificationToken();
	Task<IList<string>> GetIgnoreUsers(string settingUser);
}
