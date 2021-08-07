using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using Newtonsoft.Json.Linq;
using Serilog;
using Polly;
using Microsoft.AspNetCore.WebUtilities;

namespace SNPN.Common
{
	public class NetworkService : INetworkService, IDisposable
	{
		private readonly AppConfiguration _config;
		private readonly HttpClient _httpClient;
		private readonly ILogger _logger;

		private readonly AsyncPolicy _retryPolicy;

		private readonly Dictionary<NotificationType, string> _notificationTypeMapping = new Dictionary<NotificationType, string>
		  {
				{ NotificationType.Badge, "wns/badge" },
				{ NotificationType.Tile, "wns/tile" },
				{ NotificationType.Toast, "wns/toast" }
		  };

		public NetworkService(AppConfiguration configuration, ILogger logger, HttpClient httpClient)
		{
			_config = configuration;
			_logger = logger;
			_httpClient = httpClient;

			//Winchatty seems to crap itself if the Expect: 100-continue header is there.
			// Should be safe to leave this for every request we make.
			_httpClient.DefaultRequestHeaders.ExpectContinue = false;
			// Handle both exceptions and return values in one policy
			HttpStatusCode[] httpStatusCodesWorthRetrying = {
				HttpStatusCode.RequestTimeout, // 408
				HttpStatusCode.InternalServerError, // 500
				HttpStatusCode.BadGateway, // 502
				HttpStatusCode.ServiceUnavailable, // 503
				HttpStatusCode.GatewayTimeout // 504
			};
			_retryPolicy = Policy
				.Handle<HttpRequestException>()
				.Or<TaskCanceledException>()
				.RetryAsync(3, (e, i) =>
				{
					_logger.Information("Exception sending notification {exception} - Retrying", e);
				});
		}

		public async Task<int> WinChattyGetNewestEventId(CancellationToken ct)
		{
			using (var res = await _httpClient.GetAsync($"{_config.WinchattyApiBase}getNewestEventId", ct))
			{
				var json = JToken.Parse(await res.Content.ReadAsStringAsync());
				return json["eventId"].ToObject<int>();
			}
		}

		public async Task<JToken> WinChattyWaitForEvent(long latestEventId, CancellationToken ct)
		{
			using (var resEvent = await _httpClient.GetAsync($"{_config.WinchattyApiBase}waitForEvent?lastEventId={latestEventId}&includeParentAuthor=1", ct))
			{
				return JToken.Parse(await resEvent.Content.ReadAsStringAsync());
			}
		}

		public async Task<XDocument> GetTileContent()
		{
			using (var fileStream = await _httpClient.GetStreamAsync("http://www.shacknews.com/rss?recent_articles=1"))
			{
				return XDocument.Load(fileStream);
			}
		}

		public async Task<bool> ReplyToNotification(string replyText, string parentId, string userName, string password)
		{
			if (string.IsNullOrWhiteSpace(parentId)) { throw new ArgumentNullException(nameof(parentId)); }
			if (string.IsNullOrWhiteSpace(userName)) { throw new ArgumentNullException(nameof(userName)); }
			if (string.IsNullOrWhiteSpace(password)) { throw new ArgumentNullException(nameof(password)); }

			var data = new Dictionary<string, string> {
				 		{ "text", replyText },
				 		{ "parentId", parentId },
				 		{ "username", userName },
				 		{ "password", password }
				 	};

			JToken parsedResponse;

			using (var formContent = new FormUrlEncodedContent(data))
			{
				using (var response = await _httpClient.PostAsync($"{_config.WinchattyApiBase}postComment", formContent))
				{
					parsedResponse = JToken.Parse(await response.Content.ReadAsStringAsync());
				}
			}


			var success = parsedResponse["result"]?.ToString().Equals("success");

			return success.HasValue && success.Value;
		}

		public async Task<ResponseResult> SendNotification(QueuedNotificationItem notification, string token)
		{
			return await _retryPolicy.ExecuteAsync(async () =>
			{
				using (var request = new HttpRequestMessage
				{
					RequestUri = new Uri(notification.Uri),
					Method = HttpMethod.Post
				})
				{

					request.Headers.Add("Authorization", $"Bearer {token}");

					request.Headers.Add("X-WNS-Type", _notificationTypeMapping[notification.Type]);

					if (notification.Group != NotificationGroups.None)
					{
						request.Headers.Add("X-WNS-Group",
							 Uri.EscapeUriString(Enum.GetName(typeof(NotificationGroups), notification.Group)));
					}

					if (!string.IsNullOrWhiteSpace(notification.Tag))
					{
						request.Headers.Add("X-WNS-Tag", Uri.EscapeUriString(notification.Tag));
					}

					if (notification.Ttl > 0)
					{
						request.Headers.Add("X-WNS-TTL", notification.Ttl.ToString());
					}

					using (var stringContent =
						 new StringContent(notification.Content.ToString(SaveOptions.DisableFormatting), Encoding.UTF8,
							  "text/xml"))
					{
						request.Content = stringContent;
						using (var response = await _httpClient.SendAsync(request))
						{
							return ProcessResponse(response);
						}
					}
				}
			});
		}

		public async Task<string> GetNotificationToken()
		{
			using (var data = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					 {"grant_type", "client_credentials"},
					 {"client_id", _config.NotificationSid},
					 {"client_secret", _config.ClientSecret},
					 {"scope", "notify.windows.com"}
				}))
			{

				using (var response = await _httpClient.PostAsync("https://login.live.com/accesstoken.srf", data))
				{
					if (response.StatusCode == HttpStatusCode.OK)
					{
						var responseJson = JToken.Parse(await response.Content.ReadAsStringAsync());

						_logger.Information("Got access token.");
						return responseJson["access_token"].Value<string>();
					}
				}
			}

			return null;
		}

		public async Task<IList<string>> GetIgnoreUsers(string settingUser)
		{
			if (string.IsNullOrWhiteSpace(settingUser)) { throw new ArgumentNullException(nameof(settingUser)); }
			return (await GetSetting(settingUser, "ignoredUsers") as JArray)?.Select(x => x.ToString()).ToList() ?? new List<string>();
		}

		private async Task<JToken> GetSetting(string settingUser, string settingName)
		{
			var data = new Dictionary<string, string> {
				 		{ "username", settingUser },
						{"client", $"werd${settingName}"}
				 	};

			JToken parsedResponse = new JObject() as JToken;

			using (var response = await _httpClient.GetAsync(QueryHelpers.AddQueryString($"{_config.WinchattyApiBase}clientData/getClientData", data)))
			{
				var apiResult = JToken.Parse(await response.Content.ReadAsStringAsync());
				var settingData = apiResult["data"].ToString();
				if(!string.IsNullOrWhiteSpace(settingData))
				{
					parsedResponse = JToken.Parse(CompressionHelper.DecompressStringFromBase64(settingData));
				}
			}

			return parsedResponse;
		}

		private ResponseResult ProcessResponse(HttpResponseMessage response)
		{
			//By default, we'll just let it die if we don't know specifically that we can try again.
			var result = ResponseResult.FailDoNotTryAgain;
			_logger.Verbose("Notification Response Code: {responseStatusCode}", response.StatusCode);
			switch (response.StatusCode)
			{
				case HttpStatusCode.OK:
					result = ResponseResult.Success;
					break;
				case HttpStatusCode.NotFound:
				case HttpStatusCode.Gone:
				case HttpStatusCode.Forbidden:
					result |= ResponseResult.RemoveUser;
					break;
				case HttpStatusCode.NotAcceptable:
					result = ResponseResult.FailTryAgain;
					break;
				case HttpStatusCode.Unauthorized:
					//Need to refresh the token, so invalidate it and we'll pick up a new one on retry.
					result = ResponseResult.FailTryAgain;
					result |= ResponseResult.InvalidateToken;
					break;
			}
			return result;
		}

		#region IDisposable Support
		private bool _disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_httpClient?.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				_disposedValue = true;
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
