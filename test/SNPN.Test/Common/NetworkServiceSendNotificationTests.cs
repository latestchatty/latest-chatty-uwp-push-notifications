using SNPN.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace SNPN.Test.Common
{
	public class NetworkServiceSendNotificationTests : NetworkServiceTestsBase
	{
		[Fact]
		async void SendNotification()
		{
			var service = this.GetMockedNetworkService(string.Empty);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationBadgeTypeToast()
		{
			var type = string.Empty;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				type = r.Headers.GetValues("X-WNS-Type").Single();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.None), "token");

			Assert.Equal("wns/toast", type);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationBadgeTypeTile()
		{
			var type = string.Empty;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				type = r.Headers.GetValues("X-WNS-Type").Single();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Tile, doc, "http://test.url", NotificationGroups.None), "token");

			Assert.Equal("wns/tile", type);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationBadgeTypeBadge()
		{
			var type = string.Empty;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				type = r.Headers.GetValues("X-WNS-Type").Single();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Badge, doc, "http://test.url", NotificationGroups.None), "token");

			Assert.Equal("wns/badge", type);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithNoGroup()
		{
			bool hasGroupHeader = false;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				hasGroupHeader = r.Headers.Contains("X-WNS-Group");
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.None), "token");

			Assert.False(hasGroupHeader);
			Assert.Equal(ResponseResult.Success, result);
		}


		[Fact]
		async void SendNotificationWithGroup()
		{
			var expected = Enum.GetName(typeof(NotificationGroups), NotificationGroups.ReplyToUser);
			var actual = string.Empty;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				actual = r.Headers.GetValues("X-WNS-Group").First();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(expected, actual);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithNoTag()
		{
			var hasTagHeader = false;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				hasTagHeader = r.Headers.Contains("X-WNS-Tag");
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.False(hasTagHeader);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithTag()
		{
			var expected = "token";
			var actual = string.Empty;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				actual = r.Headers.GetValues("X-WNS-Tag").First();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser, expected), "token");

			Assert.Equal(expected, actual);
			Assert.Equal(ResponseResult.Success, result);
		}


		[Fact]
		async void SendNotificationWithNoTtl()
		{
			var hasHeader = false;
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				hasHeader = r.Headers.Contains("X-WNS-TTL");
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.False(hasHeader);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationWithTtl()
		{
			var expected = 100;
			var actual = "0";
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.OK, (r, ct) =>
			{
				actual = r.Headers.GetValues("X-WNS-TTL").First();
			});
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser, null, expected), "token");

			Assert.Equal(expected.ToString(), actual);
			Assert.Equal(ResponseResult.Success, result);
		}

		[Fact]
		async void SendNotificationNotFound()
		{
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.NotFound);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.RemoveUser | ResponseResult.FailDoNotTryAgain, result);
		}

		[Fact]
		async void SendNotificationGone()
		{
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.Gone);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.RemoveUser | ResponseResult.FailDoNotTryAgain, result);
		}

		[Fact]
		async void SendNotificationForbidden()
		{
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.Forbidden);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.RemoveUser | ResponseResult.FailDoNotTryAgain, result);
		}

		[Fact]
		async void SendNotificationNotAcceptible()
		{
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.NotAcceptable);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.FailTryAgain, result);
		}

		[Fact]
		async void SendNotificationUnauthorized()
		{
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.Unauthorized);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.InvalidateToken | ResponseResult.FailTryAgain, result);
		}

		[Fact]
		async void SendNotificationUnhandledCode()
		{
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.ProxyAuthenticationRequired);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

			Assert.Equal(ResponseResult.FailDoNotTryAgain, result);
		}
	}
}
