using Moq;
using SNPN.Common;
using SNPN.Data;
using SNPN.Monitor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Serilog;
using SNPN.Model;
using System.Xml.Linq;

namespace SNPN.Test.Monitor
{
	public class NewEventHandlerTests
	{
		private NotificationUser GetNotificationUser()
		{
			return new NotificationUser()
			{
				DateAdded = DateTime.UtcNow,
				Id = 0,
				NotifyOnUserName = 1,
				UserName = "asdf"
			};
		}

		private List<DeviceInfo> GetDeviceInfo()
		{
			return new List<DeviceInfo>
				{
					new DeviceInfo()
					{
						DeviceId = "asdf",
						NotificationUri = "http://thisis.fake"
					}
				};
		}

		private List<DeviceInfo> GetDeviceInfos()
		{
			return new List<DeviceInfo>
				{
					new DeviceInfo()
					{
						DeviceId = "asdf",
						NotificationUri = "http://thisis.fake"
					},
					new DeviceInfo()
					{
						DeviceId = "asdf1",
						NotificationUri = "http://thisis.fake"
					}
				};
		}

		private NewPostEvent GetPostEvent()
		{
			return new NewPostEvent(12345, "asdf", new Post()
			{
				Author = "testAuth",
				Body = "Mentioning user asdf hello?",
				Category = "ontopic",
				Date = DateTime.UtcNow,
				Id = 12345,
				ParentId = 0,
				ThreadId = 1234
			});
		}

		private INetworkService GetIgnoreUsersNetworkServiceMock(List<string> usersToReturn = null)
		{
			var mock = new Mock<INetworkService>();
			mock.Setup(ns => ns.GetIgnoreUsers(It.IsAny<string>()))
				.Returns(Task.FromResult((IList<string>)(usersToReturn ?? new List<string>())));
			return mock.Object;
		}

		[Fact]
		public async Task MentionTest()
		{
			var notificationMoq = new Mock<INotificationService>();
			var repo = new Mock<IUserRepo>();
			var logger = new Mock<ILogger>();
			var countNotificationsAdded = 0;

			repo.Setup(r => r.GetAllUserNamesForNotification())
				.Returns(Task.FromResult(new List<string> { "asdf" }));

			repo.Setup(r => r.FindUser("asdf"))
				.Returns(Task.FromResult(GetNotificationUser()));

			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult(GetDeviceInfo()));

			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<Post>(), It.IsAny<NotificationMatchType>(), It.IsAny<string>(), It.IsAny<string>(), NotificationGroups.ReplyToUser, It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);

			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object, GetIgnoreUsersNetworkServiceMock());

			var newEvent = GetPostEvent();
			newEvent.ParentAuthor = "notasdf";

			await handler.ProcessEvent(newEvent);
			Assert.Equal(1, countNotificationsAdded);
		}

		[Fact]
		public async Task ReplyTest()
		{
			var notificationMoq = new Mock<INotificationService>();
			var repo = new Mock<IUserRepo>();
			var logger = new Mock<ILogger>();
			var countNotificationsAdded = 0;

			repo.Setup(r => r.GetAllUserNamesForNotification())
				.Returns(Task.FromResult(new List<string>()));

			repo.Setup(r => r.FindUser("asdf"))
				.Returns(Task.FromResult(GetNotificationUser()));

			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult(GetDeviceInfo()));

			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<Post>(), It.IsAny<NotificationMatchType>(), It.IsAny<string>(), It.IsAny<string>(), NotificationGroups.ReplyToUser, It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);

			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object, GetIgnoreUsersNetworkServiceMock());

			var newEvent = GetPostEvent();

			await handler.ProcessEvent(newEvent);
			Assert.Equal(1, countNotificationsAdded);
		}

		[Fact]
		public async Task NoDeviceInfoReplyTest()
		{
			var notificationMoq = new Mock<INotificationService>();
			var repo = new Mock<IUserRepo>();
			var logger = new Mock<ILogger>();
			var countNotificationsAdded = 0;

			repo.Setup(r => r.GetAllUserNamesForNotification())
				.Returns(Task.FromResult(new List<string>()));

			repo.Setup(r => r.FindUser("asdf"))
				.Returns(Task.FromResult(GetNotificationUser()));

			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult<List<DeviceInfo>>(null));

			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<Post>(), It.IsAny<NotificationMatchType>(), It.IsAny<string>(), It.IsAny<string>(), NotificationGroups.ReplyToUser, It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);

			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object, GetIgnoreUsersNetworkServiceMock());

			var newEvent = GetPostEvent();

			await handler.ProcessEvent(newEvent);
			Assert.Equal(0, countNotificationsAdded);
		}

		[Fact]
		public async Task MultiReplyTest()
		{
			var notificationMoq = new Mock<INotificationService>();
			var repo = new Mock<IUserRepo>();
			var logger = new Mock<ILogger>();
			var countNotificationsAdded = 0;

			repo.Setup(r => r.GetAllUserNamesForNotification())
				.Returns(Task.FromResult(new List<string>()));

			repo.Setup(r => r.FindUser("asdf"))
				.Returns(Task.FromResult(GetNotificationUser()));

			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult(GetDeviceInfos()));

			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<Post>(), It.IsAny<NotificationMatchType>(), It.IsAny<string>(), It.IsAny<string>(), NotificationGroups.ReplyToUser, It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);

			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object, GetIgnoreUsersNetworkServiceMock());

			var newEvent = GetPostEvent();

			await handler.ProcessEvent(newEvent);
			Assert.Equal(2, countNotificationsAdded);
		}

		[Fact]
		public async Task SelfReply()
		{
			var notificationMoq = new Mock<INotificationService>();
			var repo = new Mock<IUserRepo>();
			var logger = new Mock<ILogger>();
			var countNotificationsAdded = 0;

			repo.Setup(r => r.GetAllUserNamesForNotification())
				.Returns(Task.FromResult(new List<string>()));

			repo.Setup(r => r.FindUser("asdf"))
				.Returns(Task.FromResult(GetNotificationUser()));

			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult(GetDeviceInfo()));

			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<Post>(), It.IsAny<NotificationMatchType>(), It.IsAny<string>(), It.IsAny<string>(), NotificationGroups.ReplyToUser, It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);

			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object, GetIgnoreUsersNetworkServiceMock());

			var newEvent = GetPostEvent();
			newEvent.Post.Author = "asdf";

			await handler.ProcessEvent(newEvent);
			Assert.Equal(0, countNotificationsAdded);
		}

		[Fact]
		public async Task MentionAndReplyTest()
		{
			var notificationMoq = new Mock<INotificationService>();
			var repo = new Mock<IUserRepo>();
			var logger = new Mock<ILogger>();
			var countNotificationsAdded = 0;

			repo.Setup(r => r.GetAllUserNamesForNotification())
				.Returns(Task.FromResult(new List<string> { "asdf" }));

			repo.Setup(r => r.FindUser("asdf"))
				.Returns(Task.FromResult(GetNotificationUser()));

			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult(GetDeviceInfo()));

			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<Post>(), It.IsAny<NotificationMatchType>(), It.IsAny<string>(), It.IsAny<string>(), NotificationGroups.ReplyToUser, It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);

			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object, GetIgnoreUsersNetworkServiceMock());

			var newEvent = GetPostEvent();

			await handler.ProcessEvent(newEvent);
			Assert.Equal(2, countNotificationsAdded);
		}

		[Fact]
		public async Task DontNotifyIfIgnoredUser()
		{
			var notificationMoq = new Mock<INotificationService>();
			var repo = new Mock<IUserRepo>();
			var logger = new Mock<ILogger>();
			var countNotificationsAdded = 0;

			repo.Setup(r => r.GetAllUserNamesForNotification())
				.Returns(Task.FromResult(new List<string>()));

			repo.Setup(r => r.FindUser("asdf"))
				.Returns(Task.FromResult(GetNotificationUser()));

			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult(GetDeviceInfo()));

			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<Post>(), It.IsAny<NotificationMatchType>(), It.IsAny<string>(), It.IsAny<string>(), NotificationGroups.ReplyToUser, It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);

			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object, GetIgnoreUsersNetworkServiceMock(new List<string>() { "TESTAuth" }));

			var newEvent = GetPostEvent();

			await handler.ProcessEvent(newEvent);
			Assert.Equal(0, countNotificationsAdded);
		}
	}
}
