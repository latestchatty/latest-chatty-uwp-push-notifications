using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications.Common
{
	public class NotificationService
	{
		private readonly AccessTokenManager accessTokenManager;
		private readonly DatabaseService dbService;

		private enum ResponseResult
		{
			Success,
			FailDoNotTryAgain,
			FailTryAgain
		}

		private Dictionary<NotificationType, string> notificationTypeMapping = new Dictionary<NotificationType, string>
		{
			{ NotificationType.Badge, "wns/badge" },
			{ NotificationType.Tile, "wns/tile" },
			{ NotificationType.Toast, "wns/toast" }
		};

		public NotificationService(AccessTokenManager accessTokenManager, DatabaseService dbService)
		{
			this.accessTokenManager = accessTokenManager;
			this.dbService = dbService;
		}

		async public Task SendNotificationData(NotificationType type, XDocument content, string notificationUri)
		{
			ResponseResult result;
			do
			{
				var token = await this.accessTokenManager.GetAccessToken();
				var client = this.CreateClient(token);
				client.DefaultRequestHeaders.Add("X-WNS-Type", this.notificationTypeMapping[type]);
				var stringContent = new StringContent(content.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
				var response = await client.PostAsync(notificationUri, stringContent);
				result = await this.ProcessResponse(response, notificationUri);
			} while (result == ResponseResult.FailTryAgain);
		}

		async public Task SendNotificationToUser(NotificationType type, XDocument content, string userName)
		{
			var collection = dbService.GetCollection();
			var user = await collection.Find(u => u.UserName.Equals(userName.ToLower())).FirstOrDefaultAsync();
			if (user != null)
			{
				foreach (var info in user.NotificationInfos)
				{
					await this.SendNotificationData(type, content, info.NotificationUri);
				}
			}
		}

		async public Task RemoveAllToastsForUser(string userName)
		{
			var collection = dbService.GetCollection();
			var user = await collection.Find(u => u.UserName.Equals(userName.ToLower())).FirstOrDefaultAsync();
			if (user != null)
			{
				var token = await this.accessTokenManager.GetAccessToken();
				var client = this.CreateClient(token);
				client.DefaultRequestHeaders.Add("X-WNS-Match", "type=wns/toast;all");
				foreach (var info in user.NotificationInfos)
				{
					ResponseResult result;
					do
					{
						var response = await client.DeleteAsync(info.NotificationUri);
						result = await this.ProcessResponse(response, info.NotificationUri);
					} while (result == ResponseResult.FailTryAgain);
				}
			}
		}

		private HttpClient CreateClient(string token)
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.ExpectContinue = false;
			client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
			return client;
		}

		async private Task<ResponseResult> ProcessResponse(HttpResponseMessage response, string uri)
		{
			//By default, we'll just let it die if we don't know specifically that we can try again.
			ResponseResult result = ResponseResult.FailDoNotTryAgain;

			switch (response.StatusCode)
			{
				case System.Net.HttpStatusCode.OK:
					result = ResponseResult.Success;
					break;
				case System.Net.HttpStatusCode.NotFound:
				case System.Net.HttpStatusCode.Gone:
					//Invalid Uri or expired, remove it from the DB
					var collection = this.dbService.GetCollection();
					var user = await collection.Find(u => u.NotificationInfos.Any(ni => ni.NotificationUri.Equals(uri))).FirstOrDefaultAsync();
					if(user != null)
					{
						var infos = user.NotificationInfos;
						var infoToRemove = infos.SingleOrDefault(x => x.NotificationUri.Equals(uri));
						if (infoToRemove != null)
						{
							infos.Remove(infoToRemove);

							var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
							var update = Builders<NotificationUser>.Update
								.CurrentDate(x => x.DateUpdated)
								.Set(x => x.NotificationInfos, infos);
							await collection.UpdateOneAsync(filter, update);
						}
					}
					break;
				default:
					break;
			}
			return result;
		}
	}
}
