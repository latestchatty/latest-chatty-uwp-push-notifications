using Newtonsoft.Json.Linq;
using SNPN.Model;
using SNPN.Monitor;
using System;
using System.Linq;
using Xunit;

namespace SNPN.Test.Monitor
{
	public class EventParserTests
	{
		private JToken GenerateEvent(string type)
		{
			return new JObject(
					new JProperty("eventId", 999),
					new JProperty("eventDate", DateTime.UtcNow),
					new JProperty("eventType", type)
				);
		}

		private JToken GenerateNewPostEvent()
		{
			var e = GenerateEvent("newPost");
			e.Last.AddAfterSelf(new JProperty("eventData",
				new JObject(
					new JProperty("postId", "12345"),
					new JProperty("parentAuthor", "test"),
					new JProperty("post", new JObject(
						new JProperty("id", "12345"),
						new JProperty("threadId", "1234"),
						new JProperty("parentId", "0"),
						new JProperty("author", "test1"),
						new JProperty("category", "ontopic"),
						new JProperty("date", "2013-12-02T01:39:00Z"),
						new JProperty("body", "This is the body")
						)
					)
				)));
			return e;
		}

		private JToken GenerateEvents()
		{
			return new JObject(
					new JProperty("lastEventId", 99999),
					new JProperty("events", 
						new JArray(GenerateNewPostEvent(), GenerateEvent("lolCountsUpdate"), GenerateNewPostEvent())
					));
		}

		[Fact]
		public void UnknownType()
		{
			var parser = new EventParser();
			var type = parser.GetEventType(GenerateEvent("derp"));
			Assert.Equal(EventType.Uknown, type);
		}

		[Fact]
		public void NewPostType()
		{
			var parser = new EventParser();
			var type = parser.GetEventType(GenerateEvent("newPost"));
			Assert.Equal(EventType.NewPost, type);
		}

		[Fact]
		public void CategoryChangeType()
		{
			var parser = new EventParser();
			var type = parser.GetEventType(GenerateEvent("categoryChange"));
			Assert.Equal(EventType.CategoryChange, type);
		}

		[Fact]
		public void ServerMessageType()
		{
			var parser = new EventParser();
			var type = parser.GetEventType(GenerateEvent("serverMessage"));
			Assert.Equal(EventType.ServerMessage, type);
		}

		[Fact]
		public void LolCountUpdateType()
		{
			var parser = new EventParser();
			var type = parser.GetEventType(GenerateEvent("lolCountsUpdate"));
			Assert.Equal(EventType.LolCountsUpdate, type);
		}

		[Fact]
		public void GetNewPostEventWrongType()
		{
			var parser = new EventParser();

			Assert.Throws<ArgumentException>(() => parser.GetNewPostEvent(GenerateEvent("wat")));
		}

		[Fact]
		public void GetNewPostPostId()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal(12345, postEvent.PostId);
		}

		[Fact]
		public void GetNewPostParentAuthor()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal("test", postEvent.ParentAuthor);
		}

		[Fact]
		public void GetNewPostPostNotNull()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.NotNull(postEvent.Post);
		}

		[Fact]
		public void GetNewPostPostPostId()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal(12345, postEvent.Post.Id);
		}

		[Fact]
		public void GetNewPostPostAuthor()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal("test1", postEvent.Post.Author);
		}

		[Fact]
		public void GetNewPostPostThreadId()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal(1234, postEvent.Post.ThreadId);
		}

		[Fact]
		public void GetNewPostPostParentId()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal(0, postEvent.Post.ParentId);
		}

		[Fact]
		public void GetNewPostPostCategory()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal("ontopic", postEvent.Post.Category);
		}

		[Fact]
		public void GetNewPostPostBody()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal("This is the body", postEvent.Post.Body);
		}


		[Fact]
		public void GetNewPostPostDate()
		{
			var parser = new EventParser();
			var postEvent = parser.GetNewPostEvent(GenerateNewPostEvent());
			Assert.Equal(new DateTime(2013, 12, 02, 01, 39, 00), postEvent.Post.Date);
		}

		[Fact]
		public void GetLatestEventId()
		{
			var parser = new EventParser();
			var eventId = parser.GetLatestEventId(GenerateEvents());
			Assert.Equal(99999, eventId);
		}

		[Fact]
		public void GetNewPostEventsCount()
		{
			var parser = new EventParser();
			var events = parser.GetNewPostEvents(GenerateEvents());
			Assert.Equal(2, events.Count());
		}
	}
}
