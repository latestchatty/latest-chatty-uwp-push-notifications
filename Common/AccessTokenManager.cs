using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Shacknews_Push_Notifications.Common;

using Microsoft.Extensions.Configuration;
using System.IO;

namespace Shacknews_Push_Notifications.Common
{
	public class AccessTokenManager : IDisposable
	{
		private string accessToken = string.Empty;
		private AppConfiguration configuration;
		SemaphoreSlim locker = new SemaphoreSlim(1);

		public AccessTokenManager(AppConfiguration config)
		{
			this.configuration = config;
		}
		public async Task<string> GetAccessToken()
		{
			try
			{
				//Make sure we don't try to get the token multiple times in a row
				await this.locker.WaitAsync();
				if (string.IsNullOrWhiteSpace(this.accessToken))
				{
					ConsoleLog.LogMessage("Getting access token.");
					using (var client = new HttpClient())
					{
						var data = new FormUrlEncodedContent(new Dictionary<string, string> {
							{ "grant_type", "client_credentials" },
							{ "client_id", configuration.NotificationSID },
							{ "client_secret", configuration.ClientSecret },
							{ "scope", "notify.windows.com" },
						});
						using (var response = await client.PostAsync("https://login.live.com/accesstoken.srf", data))
						{
							if (response.StatusCode == System.Net.HttpStatusCode.OK)
							{
								var responseJson = JToken.Parse(await response.Content.ReadAsStringAsync());
								if (responseJson["access_token"] != null)
								{
									this.accessToken = responseJson["access_token"].Value<string>();
									ConsoleLog.LogMessage($"Got access token.");
								}
							}
						}
					}
				}
			}
			finally
			{
				this.locker.Release();
			}
			return this.accessToken;
		}

		public async Task<string> RefreshAccessToken()
		{
			this.InvalidateToken();
			return await this.GetAccessToken();
		}

		public void InvalidateToken()
		{
			this.accessToken = string.Empty;
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					this.locker.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~AccessTokenManager() {
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
