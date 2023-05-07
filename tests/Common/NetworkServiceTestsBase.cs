using Moq;
using Moq.Protected;
using SNPN.Common;
using SNPN.Data;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SNPN.Test.Common
{
	public abstract class NetworkServiceTestsBase
	{
		#region Setup Helpers
		protected AppConfiguration GetAppConfig()
		{
			var config = new AppConfiguration();
			config.WinchattyApiBase = "http://testApi/";
			return config;
		}

		protected Mock<HttpMessageHandler> GetMessageHandlerMock(string returnContent, HttpStatusCode statusCode, Action<HttpRequestMessage, CancellationToken> onCalled = null)
		{
			var handler = new Mock<HttpMessageHandler>();
			var setup = handler.Protected()
				.Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
			setup.Returns(() =>
			{
				var content = returnContent;
				var contentBuffer = new StringContent(content);
				var message = new HttpResponseMessage();
				message.Content = contentBuffer;
				message.StatusCode = statusCode;
				return Task.FromResult(message);
			});
			if (onCalled != null)
			{
				setup.Callback(onCalled);
			}
			return handler;
		}

		protected NetworkService GetMockedNetworkService(string callReturn, HttpStatusCode statusCode = HttpStatusCode.OK, Action<HttpRequestMessage, CancellationToken> onCalled = null)
		{
			var logger = new Mock<Serilog.ILogger>();
			var config = GetAppConfig();
			var handler = GetMessageHandlerMock(callReturn, statusCode, onCalled);
			var repo = new UserRepo(logger.Object, config);

			var service = new NetworkService(config, logger.Object, new HttpClient(handler.Object), repo, null);
			return service;
		}
		#endregion
	}
}
