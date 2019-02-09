using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Net;

namespace SNPN.Test.Common
{
	public class NetworkServiceTests: NetworkServiceTestsBase
	{
		[Fact]
		async Task WinChattyGetNewestEventId()
		{
			var service = GetMockedNetworkService("{ \"eventId\": \"12345\" }");
			var result = await service.WinChattyGetNewestEventId(new CancellationToken());

			Assert.Equal(12345, result);
		}

		[Fact]
		async Task WinChattyWaitForEvent()
		{
			var service = GetMockedNetworkService("{ \"eventId\": \"12345\" }");
			var result = await service.WinChattyWaitForEvent(1234, new CancellationToken());

			Assert.NotNull(result);
			Assert.Equal("12345", result["eventId"].ToString());
		}

		[Fact]
		async void GetTileContent()
		{
			var service = GetMockedNetworkService("<xml></xml>");

			var xDoc = await service.GetTileContent();

			Assert.NotNull(xDoc);
			Assert.Equal("xml", xDoc.Root?.Name.LocalName);
		}

		[Fact]
		async void ReplyToNotificationParentIdException()
		{
			var service = GetMockedNetworkService("{ \"result\": \"success\" }");

			await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ReplyToNotification("", "", "hello", "world"));
		}

		[Fact]
		async void ReplyToNotificationUserNameException()
		{
			var service = GetMockedNetworkService("{ \"result\": \"success\" }");

			await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ReplyToNotification("", "hello", "", "world"));
		}

		[Fact]
		async void ReplyToNotificationPasswordException()
		{
			var service = GetMockedNetworkService("{ \"result\": \"success\" }");

			await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ReplyToNotification("", "hello", "world", ""));
		}

		[Fact]
		async void ReplyToNotification()
		{
			var service = GetMockedNetworkService("{ \"result\": \"success\" }");

			var result = await service.ReplyToNotification("asldjfk", "1234", "asdf", "asdlfkj");

			Assert.Equal(true, result);
		}

		[Fact]
		async void ReplyToNotificationFailed()
		{
			var service = GetMockedNetworkService("{ \"result\": \"error\" }");

			var result = await service.ReplyToNotification("asldjfk", "1234", "asdf", "asdlfkj");

			Assert.Equal(false, result);
		}

		[Fact]
		async void GetNotificationToken()
		{
			var tokenValue = "EgAcAQMAAAAALYAAY/c+Huwi3Fv4Ck10UrKNmtxRO6Njk2MgA=";
			var service = GetMockedNetworkService(@"{
				""access_token"":""" + tokenValue + @""", 
				""token_type"":""bearer""
			}");
			var result = await service.GetNotificationToken();

			Assert.Equal(tokenValue, result);
		}

		[Fact]
		async void GetNotificationTokenBadRequest()
		{
			var service = GetMockedNetworkService(string.Empty, HttpStatusCode.BadRequest);
			var result = await service.GetNotificationToken();

			Assert.Null(result);
		}
	}
}
