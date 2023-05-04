using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Text.Json;
using Serilog;
using Polly;
using Microsoft.AspNetCore.WebUtilities;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using SNPN.Data;

namespace SNPN.Common
{
	public class NetworkService : INetworkService, IDisposable
	{
		private readonly AppConfiguration _config;
		private readonly HttpClient _httpClient;
		private readonly ILogger _logger;
		private readonly IUserRepo _userRepo;

		private readonly AsyncPolicy _retryPolicy;

		private static Object firebaseAppLock = new Object();

		private readonly Dictionary<NotificationType, string> _notificationTypeMapping = new Dictionary<NotificationType, string>
		  {
				{ NotificationType.Badge, "wns/badge" },
				{ NotificationType.Tile, "wns/tile" },
				{ NotificationType.Toast, "wns/toast" }
		  };

		public NetworkService(AppConfiguration configuration, ILogger logger, HttpClient httpClient, IUserRepo userRepo)
		{
			_config = configuration;
			_logger = logger;
			_httpClient = httpClient;
			_userRepo = userRepo;

			// Setup Firebase Default App with credentials only once.
			lock(firebaseAppLock) {
				var fcmJSON = Environment.GetEnvironmentVariable("FCM_KEY_JSON");
				if(fcmJSON != null) {
					if (FirebaseApp.DefaultInstance == null) {
						FirebaseApp.Create(new AppOptions()
							{
								Credential = GoogleCredential.FromJson(fcmJSON)
							});
						_logger.Information("FirebaseApp initialization complete.");
					}
				}
				else {
					_logger.Warning("The environment variable FCM_KEY_JSON could not be found, FCM messaging will not work!");
				}
			}

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
				var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
				return json.RootElement.GetProperty("eventId").GetInt32();
			}
		}

		public async Task<JsonElement> WinChattyWaitForEvent(long latestEventId, CancellationToken ct)
		{
			using (var resEvent = await _httpClient.GetAsync($"{_config.WinchattyApiBase}waitForEvent?lastEventId={latestEventId}&includeParentAuthor=1", ct))
			{
				return JsonDocument.Parse(await resEvent.Content.ReadAsStringAsync()).RootElement;
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

			JsonDocument parsedResponse;

			using (var formContent = new FormUrlEncodedContent(data))
			{
				using (var response = await _httpClient.PostAsync($"{_config.WinchattyApiBase}postComment", formContent))
				{
					parsedResponse = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
				}
			}


			var success = parsedResponse.RootElement.GetProperty("result").GetString().Equals("success");

			return success;
		}

		public async Task<ResponseResult> SendNotificationWNS(QueuedNotificationItem notification, string token)
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
							 Uri.EscapeDataString(Enum.GetName(typeof(NotificationGroups), notification.Group)));
					}

					if (!string.IsNullOrWhiteSpace(notification.Tag))
					{
						request.Headers.Add("X-WNS-Tag", Uri.EscapeDataString(notification.Tag));
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

		public async Task<ResponseResult> SendNotificationFCM(QueuedNotificationItem notification)
		{
			return await _retryPolicy.ExecuteAsync(async () =>
			{
				_logger.Information("SendNotificationFCM {notificationUri}", LogHelper.GetAbbreviatedString(notification.Uri));
				var message = new Message()
				{
					Data = new Dictionary<string, string>()
					{
					["type"] = "general",
					["username"] = notification.Post.Author,
					["title"] = notification.Title,
					["text"] = notification.Message,
					["nlsid"] = notification.Post.Id.ToString(),
					["parentid"] = notification.Post.ParentId.ToString(),
					},
					Token = notification.Uri.Replace("fcm://", ""),
				};
				var messaging = FirebaseMessaging.DefaultInstance;
				try {
					var result = await messaging.SendAsync(message);					
					_logger.Information("SendNotificationFCM result: {result}", result);
				}
				catch (FirebaseMessagingException e) {
					_logger.Error("SendNotificationFCM FirebaseMessagingException when trying to send to {notificationUri} {ErrorCode}", notification.Uri, e.ErrorCode);
					if (e.ErrorCode == ErrorCode.NotFound) {
						_logger.Warning("Removing FCM device with Uri {notificationUri} from DB", notification.Uri);
						await _userRepo.DeleteDeviceByUri(notification.Uri);
					}
					else {
						throw;
					}
				}
				return ResponseResult.Success;
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
						var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

						_logger.Information("Got access token.");
						return responseJson.RootElement.GetProperty("access_token").GetString();
					}
				}
			}

			return null;
		}

		public async Task<IList<string>> GetIgnoreUsers(string settingUser)
		{
			if (string.IsNullOrWhiteSpace(settingUser)) { throw new ArgumentNullException(nameof(settingUser)); }
			return (await GetSetting(settingUser, "ignoredUsers"))?.RootElement.EnumerateArray().Select(x => x.GetString()).ToList() ?? new List<string>();
		}

		private async Task<JsonDocument> GetSetting(string settingUser, string settingName)
		{
			var data = new Dictionary<string, string> {
				 		{ "username", settingUser },
						{"client", $"werd{settingName}"}
				 	};

			JsonDocument parsedResponse = null;

			using (var response = await _httpClient.GetAsync(QueryHelpers.AddQueryString($"{_config.WinchattyApiBase}clientData/getClientData", data)))
			{
				var apiResult = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
				var settingData = apiResult.RootElement.GetProperty("data").GetString();
				if(!string.IsNullOrWhiteSpace(settingData))
				{
					parsedResponse = JsonDocument.Parse(CompressionHelper.DecompressStringFromBase64(settingData));
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
