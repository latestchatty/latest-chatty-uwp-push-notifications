using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SNPN.Common
{
	interface INetworkService
	{
		Task<int> WinChattyGetNewestEventId(CancellationToken ct);

		Task<JToken> WinChattyWaitForEventAsync(long latestEventId, CancellationToken ct);
	}
}
