using MongoDB.Driver;
using Nancy;
using Nancy.ModelBinding;
using Newtonsoft.Json.Linq;
using Shacknews_Push_Notifications.Common;
using Shacknews_Push_Notifications.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Shacknews_Push_Notifications
{
	public class Endpoint : NancyModule
	{
		private readonly NotificationService notificationService;
		private readonly DatabaseService dbService;

		public Endpoint(NotificationService notificationService, DatabaseService dbService)
		{
			this.notificationService = notificationService;
			this.dbService = dbService;
			Post["/register"] = this.RegisterDevice;
			Post["/deregister"] = this.DeregisterDevice;
			Post["/resetcount"] = this.ResetCount;
			Post["/replyToNotification"] = this.ReplyToNotification;
			Post["/removeNotification"] = this.RemoveNotification;
			Get["/openReplyNotifications"] = this.GetOpenReplyNotifications;
			Get["/test"] = x => "Hello world!";
			Get["tileContent"] = this.GetTileContent;
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

		private class UserNameArgs
		{
			public string UserName { get; set; }
		}

		private class ReplyToNotificationArgs
		{
			public string UserName { get; set; }
			public string Password { get; set; }
			public string ParentId { get; set; }
			public string Text { get; set; }
		}

		private class RemoveNotificationArgs
		{
			public string UserName { get; set; }
			public string Group { get; set; }
			public string Tag { get; set; }
		}
		#endregion
		private dynamic GetTileContent(dynamic arg)
		{
			try
			{
				//TODO: Cache this in the DB so we don't have to hit shacknews every time.
				Console.WriteLine("Retrieving tile content.");
				var xDoc = XDocument.Load("http://www.shacknews.com/rss?recent_articles=1");
				var items = xDoc.Descendants("item");
				var itemsObj = items.Select(i => new
				{
					Title = i.Element("title").Value,
					PublishDate = DateTime.Parse(i.Element("pubDate").Value.Replace("PDT", "").Trim()),
					Author = i.Element("author").Value
				}).OrderByDescending(i => i.PublishDate).Take(3);

				var item = itemsObj.FirstOrDefault();

				if (item == null) return string.Empty;

				var visualElement = new XElement("visual", new XAttribute("version", "2"));
				var tileElement = new XElement("tile", visualElement);

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileWide310x150Text09"), new XAttribute("fallback", "TileWideText09"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title)));

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileSquare150x150Text02"), new XAttribute("fallback", "TileSquareText02"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title)));

				visualElement.Add(new XElement("binding", new XAttribute("template", "TileSquare310x310TextList03"),
					new XElement("text", new XAttribute("id", "1"), $"{item.Author} posted"),
					new XElement("text", new XAttribute("id", "2"), item.Title),
					new XElement("text", new XAttribute("id", "3"), $"{ itemsObj.ElementAt(1).Author} posted"),
					new XElement("text", new XAttribute("id", "4"), itemsObj.ElementAt(1).Title),
					new XElement("text", new XAttribute("id", "5"), $"{itemsObj.ElementAt(2).Author} posted"),
					new XElement("text", new XAttribute("id", "6"), itemsObj.ElementAt(2).Title)));
				var doc = new XDocument(tileElement);

				return doc.ToString(SaveOptions.DisableFormatting);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Excepiton retrieving tile content : {ex}");
			}
			return string.Empty;
		}

		private async Task<dynamic> GetOpenReplyNotifications(dynamic arg)
		{
			try
			{
				Console.WriteLine("Getting open reply notifications.");
				var e = this.Bind<UserNameArgs>();

				Console.WriteLine($"Getting open reply notifications for user {e.UserName}.");
				var collection = this.dbService.GetCollection();

				var user = await collection.Find(u => u.UserName.Equals(e.UserName.ToLower())).FirstOrDefaultAsync();
				if (user != null)
				{
					return new { data = user.ReplyEntries };
				}
				else
				{
					Console.WriteLine($"User {e.UserName} not found when getting open reply notifications.");
					return new { status = "error", message = "User not found." };
				}
			}
			catch (Exception ex)
			{
				//TODO: Log exception
				Console.WriteLine($"!!!!Exception in {nameof(GetOpenReplyNotifications)}: {ex.ToString()}");
				return new { status = "error" };
			}
		}

		async private Task<dynamic> RemoveNotification(dynamic arg)
		{
			try
			{
				Console.WriteLine("Removing notification.");
				var e = this.Bind<RemoveNotificationArgs>();

				var collection = this.dbService.GetCollection();

				var user = await collection.Find(u => u.UserName.Equals(e.UserName)).FirstOrDefaultAsync();
				if (user != null)
				{
					NotificationGroups group;
					if (!Enum.TryParse(e.Group, out group))
					{
						group = NotificationGroups.None;
					}

					//Only decrement badge count if we're removing a reply notification.
					if (group == NotificationGroups.ReplyToUser)
					{
						int postId;
						if (int.TryParse(e.Tag, out postId))
						{
							//Update DB Count
							if (user.ReplyEntries == null)
							{
								user.ReplyEntries = new List<ReplyEntry>();
							}
							else
							{
								var entry = user.ReplyEntries.SingleOrDefault(re => re.PostId == postId);
								if (entry != null)
								{
									user.ReplyEntries.Remove(entry);
								}
							}
							var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
							var update = Builders<NotificationUser>.Update
								.CurrentDate(x => x.DateUpdated)
								.Set(x => x.ReplyEntries, user.ReplyEntries);
							await collection.UpdateOneAsync(filter, update);
							//Update badge to reflect new count
							var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", user.ReplyEntries.Count)));
							await this.notificationService.QueueNotificationToUser(NotificationType.Badge, badgeDoc, user.UserName);
						}
					}

					//Delete notification for this reply from other devices.
					await this.notificationService.QueueNotificationToUser(NotificationType.RemoveToasts, null, user.UserName, group, e.Tag);
				}
				else
				{
					Console.WriteLine($"User {e.UserName} not found when removing.");
					return new { status = "error", message = "User not found." };
				}
				return new { status = "success" };
			}
			catch (Exception ex)
			{
				//TODO: Log exception
				Console.WriteLine($"!!!!Exception in {nameof(RemoveNotification)}: {ex.ToString()}");
				return new { status = "error" };
			}
		}

		async private Task<dynamic> ReplyToNotification(dynamic arg)
		{
			try
			{
				Console.WriteLine("Replying to notification.");
				var e = this.Bind<ReplyToNotificationArgs>();

				using (var request = new HttpClient())
				{
					var data = new Dictionary<string, string> {
						{ "text", e.Text },
						{ "parentId", e.ParentId },
						{ "username", e.UserName },
						{ "password", e.Password }
					};

					//Winchatty seems to crap itself if the Expect: 100-continue header is there.
					request.DefaultRequestHeaders.ExpectContinue = false;

					var formContent = new FormUrlEncodedContent(data);

					var response = await request.PostAsync($"{ConfigurationManager.AppSettings["winChattyApiBase"]}postComment", formContent);
					var parsedResponse = JToken.Parse(await response.Content.ReadAsStringAsync());
					var success = parsedResponse["result"]?.ToString().Equals("success");
					if (success.HasValue && success.Value)
					{
						var collection = this.dbService.GetCollection();

						var user = await collection.Find(u => u.UserName.Equals(e.UserName.ToLower())).FirstOrDefaultAsync();
						if (user != null)
						{
							int postId;
							if (int.TryParse(e.ParentId, out postId))
							{
								//Update DB Count
								if (user.ReplyEntries == null)
								{
									user.ReplyEntries = new List<ReplyEntry>();
								}
								else
								{
									var entry = user.ReplyEntries.SingleOrDefault(re => re.PostId == postId);
									if (entry != null)
									{
										user.ReplyEntries.Remove(entry);
									}
								}
								var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
								var update = Builders<NotificationUser>.Update
									.CurrentDate(x => x.DateUpdated)
									.Set(x => x.ReplyEntries, user.ReplyEntries);
								await collection.UpdateOneAsync(filter, update);
								//Update badge to reflect new count
								var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", user.ReplyEntries.Count)));
								await this.notificationService.QueueNotificationToUser(NotificationType.Badge, badgeDoc, user.UserName);
							}
							//Delete notification for this reply from other devices.
							await this.notificationService.QueueNotificationToUser(NotificationType.RemoveToasts, null, user.UserName, NotificationGroups.ReplyToUser, e.ParentId);
						}
						else
						{
							Console.WriteLine($"User {e.UserName} not found when replying.");
							return new { status = "error", message = "User not found." };
						}
						return new { status = "success" };
					}
					else
					{
						return new { status = "error" };
					}
				}
			}
			catch (Exception ex)
			{
				//TODO: Log exception
				Console.WriteLine($"!!!!Exception in {nameof(ReplyToNotification)}: {ex.ToString()}");
				return new { status = "error" };
			}
		}

		async private Task<dynamic> DeregisterDevice(dynamic arg)
		{
			try
			{
				Console.WriteLine("Deregister device.");
				var e = this.Bind<DeregisterArgs>();

				var collection = this.dbService.GetCollection();

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
			catch (Exception ex)
			{
				//TODO: Log exception
				Console.WriteLine($"!!!!Exception in {nameof(DeregisterDevice)}: {ex.ToString()}");
				return new { status = "error" };
			}
		}

		async private Task<dynamic> RegisterDevice(dynamic arg)
		{
			try
			{
				Console.WriteLine("Register device.");
				var e = this.Bind<RegisterArgs>();
				var collection = this.dbService.GetCollection();

				var user = await collection.Find(u => u.UserName.Equals(e.UserName.ToLower())).FirstOrDefaultAsync();
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
						UserName = e.UserName.ToLower(),
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
			catch (Exception ex)
			{
				//TODO: Log exception
				Console.WriteLine($"!!!!Exception in {nameof(RegisterDevice)}: {ex.ToString()}");
				return new { status = "error" };
			}
		}

		async private Task<dynamic> ResetCount(dynamic arg)
		{
			try
			{
				Console.WriteLine("Reset count.");
				var e = this.Bind<UserNameArgs>();
				var collection = this.dbService.GetCollection();

				var user = await collection.Find(u => u.UserName.Equals(e.UserName.ToLower())).FirstOrDefaultAsync();
				if (user != null)
				{
					//Update user
					var badgeDoc = new XDocument(new XElement("badge", new XAttribute("value", 0)));
					await this.notificationService.QueueNotificationToUser(NotificationType.Badge, badgeDoc, user.UserName);
					await this.notificationService.RemoveToastsForUser(user.UserName);
					var filter = Builders<NotificationUser>.Filter.Eq("_id", user._id);
					var update = Builders<NotificationUser>.Update
						.CurrentDate(x => x.DateUpdated)
						.Set(x => x.ReplyEntries, new List<ReplyEntry>());
					await collection.UpdateOneAsync(filter, update);
				}
				else
				{
					Console.WriteLine($"User {e.UserName} not found");
					return new { status = "error", message = "User not found." };
				}
				return new { status = "success" };
			}
			catch (Exception ex)
			{
				//TODO: Log exception
				Console.WriteLine($"!!!!Exception in {nameof(ResetCount)}: {ex.ToString()}");
				return new { status = "error" };
			}
		}
	}
}
