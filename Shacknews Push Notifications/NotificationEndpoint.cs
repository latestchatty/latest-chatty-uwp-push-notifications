using MongoDB.Driver;
using Nancy;
using Nancy.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shacknews_Push_Notifications
{
	public class NotificationEndpoint : NancyModule
	{
		public NotificationEndpoint()
		{
			Post["/register"] = this.RegisterDevice;
			Post["/deregister"] = this.DeregisterDevice;
			Post["/resetcount"] = this.ResetCount;
		}

		#region Event Bind Classes
		private class RegisterArgs
		{
			public string UserName { get; set; }
			public string DeviceId { get; set; }
			public string ChannelUri { get; set; }
		}

		private class DeregisterArgs
		{
			public string DeviceId { get; set; }
		}

		private class ResetCountArgs
		{
			public string UserName { get; set; }
		}
		#endregion

		async private Task<dynamic> DeregisterDevice(dynamic arg)
		{
			try
			{
				var e = this.Bind<DeregisterArgs>();

				var collection = this.GetDbCollection();

				var userName = arg.userName.ToString().ToLower() as string;
				var user = await collection.Find(u => u.NotificationInfos.Any(ni => ni.DeviceId.Equals(e.DeviceId))).FirstOrDefaultAsync();
				if (user != null)
				{
					var infos = user.NotificationInfos;
					var infoToRemove = infos.SingleOrDefault(x => x.DeviceId.Equals(e.DeviceId));
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
				return new { status = "success" };
			}
			catch (Exception)
			{
				//TODO: Log exception
				return new { status = "error" };
			}
		}

		async private Task<dynamic> RegisterDevice(dynamic arg)
		{
			try
			{
				var e = this.Bind<RegisterArgs>();
				var collection = this.GetDbCollection();

				var user = await collection.Find(u => u.UserName.Equals(e.UserName)).FirstOrDefaultAsync();
				if (user != null)
				{
					//Update user
					var infos = user.NotificationInfos;
					var info = infos.SingleOrDefault(x => x.DeviceId.Equals(e.DeviceId));
					if (info != null)
					{
						info.NotificationUri = e.ChannelUri;
					}
					else
					{
						infos.Add(new NotificationInfo()
						{
							DeviceId = e.DeviceId,
							NotificationUri = e.ChannelUri
						});
					}
					var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
					var update = Builders<NotificationUser>.Update
						.CurrentDate(x => x.DateUpdated)
						.Set(x => x.NotificationInfos, infos);
					await collection.UpdateOneAsync(filter, update);
				}
				else
				{
					//Insert user
					user = new NotificationUser()
					{
						UserName = e.UserName,
						DateUpdated = DateTime.UtcNow,
						NotificationInfos = new List<NotificationInfo>(new[]
						{
							new NotificationInfo()
							{
								DeviceId = e.DeviceId,
								NotificationUri = e.ChannelUri
							}
						})
					};
					await collection.InsertOneAsync(user);
				}
				return new { status = "success" };
			}
			catch (Exception)
			{
				//TODO: Log exception
				return new { status = "error" };
			}
		}

		async private Task<dynamic> ResetCount(dynamic arg)
		{
			try
			{
				var e = this.Bind<ResetCountArgs>();
				var collection = this.GetDbCollection();

				var user = await collection.Find(u => u.UserName.Equals(e.UserName)).FirstOrDefaultAsync();
				if (user != null)
				{
					//Update user

					var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
					var update = Builders<NotificationUser>.Update
						.CurrentDate(x => x.DateUpdated)
						.Set(x => x.ReplyCount, 0);
					await collection.UpdateOneAsync(filter, update);
				}
				else
				{
					return new { status = "error", message = "User not found." };
				}
				return new { status = "success" };
			}
			catch (Exception)
			{
				//TODO: Log exception
				return new { status = "error" };
			}
		}


		private IMongoCollection<NotificationUser> GetDbCollection()
		{
			var dbClient = new MongoClient("mongodb://192.168.1.216:27017/"); //TODO: Read from configuration
			var db = dbClient.GetDatabase("notifications");

			return db.GetCollection<NotificationUser>("notificationUsers");
		}
	}
}
