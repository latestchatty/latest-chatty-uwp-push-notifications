using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SNPN.Common
{
	public class NetworkService : INetworkService, IDisposable
	{
		private AppConfiguration config;
		private HttpClient httpClient;
		private ILogger logger;

		public NetworkService(AppConfiguration configuration, ILogger logger, HttpClientHandler httpHandler)
		{
			this.config = configuration;
			this.logger = logger;
			this.httpClient = new HttpClient(httpHandler);
		}

		public async Task<int> WinChattyGetNewestEventId(CancellationToken ct)
		{
			using (var res = await this.httpClient.GetAsync($"{this.config.WinchattyAPIBase}getNewestEventId", ct))
			{
				var json = JToken.Parse(await res.Content.ReadAsStringAsync());
				return json["eventId"].ToObject<int>();
			}
		}
	
		public async Task<JToken> WinChattyWaitForEventAsync(long latestEventId, CancellationToken ct)
		{
			using (var resEvent = await httpClient.GetAsync($"{this.config.WinchattyAPIBase}waitForEvent?lastEventId={latestEventId}&includeParentAuthor=1", ct))
			{
				return JToken.Parse(await resEvent.Content.ReadAsStringAsync());
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					this.httpClient?.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~NetworkService() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
