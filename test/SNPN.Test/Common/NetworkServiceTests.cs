using Moq;
using Moq.Protected;
using SNPN.Common;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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

		#region Setup Helpers
		private AppConfiguration GetAppConfig()
		{
			var config = new AppConfiguration();
			config.WinchattyApiBase = "http://testApi/";
			return config;
		}

		private Mock<HttpMessageHandler> GetMessageHandlerMock(string returnContent)
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
					return Task.FromResult(message);
				});
			return handler;
		}

		private NetworkService GetMockedNetworkService(string callReturn)
		{
			var logger = new Mock<Serilog.ILogger>();
			var config = this.GetAppConfig();
			var handler = GetMessageHandlerMock(callReturn);

			var service = new NetworkService(config, logger.Object, handler.Object);
			return service;
		}
		#endregion
	}
}
