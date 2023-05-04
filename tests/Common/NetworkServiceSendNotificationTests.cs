using Moq;
using Moq.Protected;
using SNPN.Common;
using SNPN.Data;
using SNPN.Model;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SNPN.Test.Common
{
	public class NetworkServiceSendNotificationTests : NetworkServiceTestsBase
	{
		[Fact]
		async void SendNotification()
		{
			var service = GetMockedNetworkService(string.Empty);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationBadgeTypeToast()
		{
			var type = string.Empty;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				type = r.Headers.GetValues("X-WNS-Type").Single();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url"), "token");

			Assert.Equal("wns/toast", type);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationBadgeTypeTile()
		{
			var type = string.Empty;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				type = r.Headers.GetValues("X-WNS-Type").Single();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Tile, doc, It.IsAny<Post>(), "http://test.url"), "token");

			Assert.Equal("wns/tile", type);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationBadgeTypeBadge()
		{
			var type = string.Empty;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				type = r.Headers.GetValues("X-WNS-Type").Single();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Badge, doc, It.IsAny<Post>(), "http://test.url"), "token");

			Assert.Equal("wns/badge", type);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithNoGroup()
		{
			bool hasGroupHeader = false;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				hasGroupHeader = r.Headers.Contains("X-WNS-Group");
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url"), "token");

			Assert.False(hasGroupHeader);
			Assert.Equal(ResponseResult.Success, result);
		}


		[Fact]
		async void SendNotificationWithGroup()
		{
			var expected = Enum.GetName(typeof(NotificationGroups), NotificationGroups.ReplyToUser);
			var actual = string.Empty;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				actual = r.Headers.GetValues("X-WNS-Group").First();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(expected, actual);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithNoTag()
		{
			var hasTagHeader = false;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				hasTagHeader = r.Headers.Contains("X-WNS-Tag");
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.False(hasTagHeader);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithTag()
		{
			var expected = "token";
			var actual = string.Empty;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				actual = r.Headers.GetValues("X-WNS-Tag").First();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser, expected), "token");

			Assert.Equal(expected, actual);
			Assert.Equal(ResponseResult.Success, result);
		}


		[Fact]
		async void SendNotificationWithNoTtl()
		{
			var hasHeader = false;
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				hasHeader = r.Headers.Contains("X-WNS-TTL");
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.False(hasHeader);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithTtl()
		{
			var expected = 100;
			var actual = "0";
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				actual = r.Headers.GetValues("X-WNS-TTL").First();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser, null, expected), "token");

			Assert.Equal(expected.ToString(), actual);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationNotFound()
		{
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.NotFound);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.RemoveUser | ResponseResult.FailDoNotTryAgain, result);
		}

		[Fact]
		async void SendNotificationGone()
		{
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.Gone);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.RemoveUser | ResponseResult.FailDoNotTryAgain, result);
		}

		[Fact]
		async void SendNotificationForbidden()
		{
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.Forbidden);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.RemoveUser | ResponseResult.FailDoNotTryAgain, result);
		}

		[Fact]
		async void SendNotificationNotAcceptible()
		{
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.NotAcceptable);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.FailTryAgain, result);
		}

		[Fact]
		async void SendNotificationUnauthorized()
		{
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.Unauthorized);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.InvalidateToken | ResponseResult.FailTryAgain, result);
		}

		[Fact]
		async void SendNotificationUnhandledCode()
		{
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.ProxyAuthenticationRequired);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.FailDoNotTryAgain, result);
		}

		[Fact]
		async void PollyRetry()
		{
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			
			var config = new AppConfiguration();
			config.WinchattyApiBase = "http://testApi/";

			var handler = new Mock<HttpMessageHandler>();
			var setup = handler.Protected()
				.SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
				.ThrowsAsync(new TaskCanceledException())
				.ReturnsAsync(new HttpResponseMessage() {
					Content = new StringContent(string.Empty),
					StatusCode = HttpStatusCode.OK
				});

			var logger = new Mock<Serilog.ILogger>();
			var repo = new Mock<IUserRepo>();

			var service = new NetworkService(config, logger.Object, new HttpClient(handler.Object), repo.Object);
			var result = await service.SendNotificationWNS(new QueuedNotificationItem(NotificationType.Toast, doc, It.IsAny<Post>(), "http://test.url", NotificationGroups.ReplyToUser), "token");

			logger.Verify(x => x.Information("Exception sending notification {exception} - Retrying", It.IsAny<Exception>()));
			
			Assert.Equal(ResponseResult.Success, result);
		}
	}
}
