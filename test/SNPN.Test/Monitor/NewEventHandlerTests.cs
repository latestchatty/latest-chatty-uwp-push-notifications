using Moq;
using SNPN.Common;
using SNPN.Data;
using SNPN.Monitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Serilog;
using SNPN.Model;
using System.Xml.Linq;

namespace SNPN.Test.Monitor
{
	public class NewEventHandlerTests
	{

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
				.Returns(Task.FromResult(new NotificationUser()
				{
					DateAdded = DateTime.UtcNow,
					Id = 0,
					NotifyOnUserName = 1,
					UserName = "asdf"
				}));
			repo.Setup(r => r.GetUserDeviceInfos(It.IsAny<NotificationUser>()))
				.Returns(Task.FromResult(new List<DeviceInfo>
				{
					new DeviceInfo()
					{
						DeviceId = "asdf",
						NotificationUri = "http://thisis.fake"
					}
				}.AsEnumerable()));
			notificationMoq.Setup(n => n.QueueNotificationData(NotificationType.Toast, It.IsAny<string>(), It.IsAny<XDocument>(), NotificationGroups.ReplyToUser, It.IsAny<string>(), It.IsAny<int>()))
				.Callback(() => countNotificationsAdded++);
			var handler = new NewEventHandler(notificationMoq.Object, repo.Object, logger.Object);
			var newEvent = new NewPostEvent(12345, "testPA", new Post()
			{
				Author = "testAuth",
				Body = "Mentioning user asdf hello?",
				Category = "ontopic",
				Date = DateTime.UtcNow,
				Id = 12345,
				ParentId = 0,
				ThreadId = 1234
			});
			await handler.ProcessEvent(newEvent);
			Assert.Equal(1, countNotificationsAdded);
		}
	}
}
