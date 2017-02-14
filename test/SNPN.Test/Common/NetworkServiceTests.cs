using Moq;
using Moq.Protected;
using SNPN.Common;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Net;

namespace SNPN.Test.Common
{
	public class NetworkServiceTests
	{
		[Fact]
		async Task WinChattyGetNewestEventId()
		{
			var service = this.GetMockedNetworkService("{ \"eventId\": \"12345\" }");
			var result = await service.WinChattyGetNewestEventId(new CancellationToken());

			Assert.Equal(12345, result);
		}

		[Fact]
		async Task WinChattyWaitForEvent()
		{
			var service = this.GetMockedNetworkService("{ \"eventId\": \"12345\" }");
			var result = await service.WinChattyWaitForEvent(1234, new CancellationToken());

			Assert.NotNull(result);
			Assert.Equal("12345", result["eventId"].ToString());
		}

		[Fact]
		async void GetTileContent()
		{
			var service = this.GetMockedNetworkService("<xml></xml>");

			var xDoc = await service.GetTileContent();

			Assert.NotNull(xDoc);
			Assert.Equal("xml", xDoc.Root.Name.LocalName);
		}

		[Fact]
		async void ReplyToNotificationParentIdException()
		{
			var service = this.GetMockedNetworkService("{ \"result\": \"success\" }");

			await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ReplyToNotification("", "", "hello", "world"));
		}

		[Fact]
		async void ReplyToNotificationUserNameException()
		{
			var service = this.GetMockedNetworkService("{ \"result\": \"success\" }");

			await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ReplyToNotification("", "hello", "", "world"));
		}

		[Fact]
		async void ReplyToNotificationPasswordException()
		{
			var service = this.GetMockedNetworkService("{ \"result\": \"success\" }");

			await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ReplyToNotification("", "hello", "world", ""));
		}

		[Fact]
		async void ReplyToNotification()
		{
			var service = this.GetMockedNetworkService("{ \"result\": \"success\" }");

			var result = await service.ReplyToNotification("asldjfk", "1234", "asdf", "asdlfkj");

			Assert.Equal(true, result);
		}

		[Fact]
		async void ReplyToNotificationFailed()
		{
			var service = this.GetMockedNetworkService("{ \"result\": \"error\" }");

			var result = await service.ReplyToNotification("asldjfk", "1234", "asdf", "asdlfkj");

			Assert.Equal(false, result);
		}

		[Fact]
		async void GetNotificationToken()
		{
			var tokenValue = "EgAcAQMAAAAALYAAY/c+Huwi3Fv4Ck10UrKNmtxRO6Njk2MgA=";
			var service = this.GetMockedNetworkService(@"{
				""access_token"":""" + tokenValue + @""", 
				""token_type"":""bearer""
			}");
			var result = await service.GetNotificationToken();

			Assert.Equal(tokenValue, result);
		}

		[Fact]
		async void GetNotificationTokenBadRequest()
		{
			var service = this.GetMockedNetworkService(string.Empty, HttpStatusCode.BadRequest);
			var result = await service.GetNotificationToken();

			Assert.Null(result);
		}

		[Fact]
		async void SendNotification()
		{
			var service = this.GetMockedNetworkService(string.Empty);
			var doc = NotificationBuilder.BuildReplyDoc(1, "Hello", "World");
			var result = await service.SendNotification(new QueuedNotificationItem(NotificationType.Toast, doc, "http://test.url", NotificationGroups.ReplyToUser), "token");

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

		#region Setup Helpers
		private AppConfiguration GetAppConfig()
		{
			var config = new AppConfiguration();
			config.WinchattyApiBase = "http://testApi/";
			return config;
		}

		private Mock<HttpMessageHandler> GetMessageHandlerMock(string returnContent, HttpStatusCode statusCode)
		{
			var handler = new Mock<HttpMessageHandler>();
			handler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
				.Returns(() =>
				{
					var content = returnContent;
					var contentBuffer = new StringContent(content);
					var message = new HttpResponseMessage();
					message.Content = contentBuffer;
					message.StatusCode = statusCode;
					return Task.FromResult(message);
				});
			return handler;
		}

		private NetworkService GetMockedNetworkService(string callReturn, HttpStatusCode statusCode = HttpStatusCode.OK)
		{
			var logger = new Mock<Serilog.ILogger>();
			var config = this.GetAppConfig();
			var handler = GetMessageHandlerMock(callReturn, statusCode);

			var service = new NetworkService(config, logger.Object, handler.Object);
			return service;
		}
		#endregion
	}
}
