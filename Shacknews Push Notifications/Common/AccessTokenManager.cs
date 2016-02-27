using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications.Common
{
	public class AccessTokenManager
	{
		private string accessToken = string.Empty;

		public async Task<string> GetAccessToken()
		{
			if (string.IsNullOrWhiteSpace(this.accessToken))
			{
				Console.WriteLine("Getting access token.");
				var client = new HttpClient();
				var data = new FormUrlEncodedContent(new Dictionary<string, string> {
					{ "grant_type", "client_credentials" },
					{ "client_id", ConfigurationManager.AppSettings["notificationSID"] },
					{ "client_secret", ConfigurationManager.AppSettings["clientSecret"] },
					{ "scope", "notify.windows.com" },
				});
				var response = await client.PostAsync("https://login.live.com/accesstoken.srf", data);
				if (response.StatusCode == System.Net.HttpStatusCode.OK)
				{
					var responseJson = JToken.Parse(await response.Content.ReadAsStringAsync());
					if (responseJson["access_token"] != null)
					{
						this.accessToken = responseJson["access_token"].Value<string>();
						Console.WriteLine($"Got access token {this.accessToken}");
					}
				}
			}
			return this.accessToken;
		}

		public async Task<string> RefreshAccessToken()
		{
			this.accessToken = string.Empty;
			return await this.GetAccessToken();
		}
	}
}
