using SNPN.Model;
using SNPN.Monitor;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SNPN.Test.Monitor
{
    public static class JsonWriterExtensions
    {
        public static void WriteEvent(this Utf8JsonWriter jsonWriter, string type)
        {
            jsonWriter.WriteNumber("eventId", 999);
            jsonWriter.WriteString("eventDate", DateTime.UtcNow);
            jsonWriter.WriteString("eventType", type);
        }

        public static void WrtiteNewPostEvent(this Utf8JsonWriter jsonWriter)
        {
            jsonWriter.WriteEvent("newPost");
            jsonWriter.WriteStartObject("eventData");
            jsonWriter.WriteNumber("postId", 12345);
            jsonWriter.WriteString("parentAuthor", "test");
            jsonWriter.WriteStartObject("post");
            jsonWriter.WriteNumber("id", 12345);
            jsonWriter.WriteNumber("threadId", 1234);
            jsonWriter.WriteNumber("parentId", 0);
            jsonWriter.WriteString("author", "test1");
            jsonWriter.WriteString("category", "ontopic");
            jsonWriter.WriteString("date", "2013-12-02T01:39:00Z");
            jsonWriter.WriteString("body", "This is the body");
            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
        }
    }

    public class EventParserTests
    {
        private JsonElement GenerateEvent(string type)
        {
            using var ms = new System.IO.MemoryStream();
            using (var jsonWriter = new Utf8JsonWriter(ms))
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WriteEvent(type);
                jsonWriter.WriteEndObject();
            }
            return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(ms.ToArray())).RootElement;
        }

        private JsonElement GenerateEvents()
        {
            using var ms = new System.IO.MemoryStream();
            using (var jsonWriter = new Utf8JsonWriter(ms))
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WriteNumber("lastEventId", 99999);
                jsonWriter.WriteStartArray("events");
				jsonWriter.WriteStartObject();
                jsonWriter.WrtiteNewPostEvent();
				jsonWriter.WriteEndObject();
				jsonWriter.WriteStartObject();
                jsonWriter.WriteEvent("lolCountsUpdate");
				jsonWriter.WriteEndObject();
				jsonWriter.WriteStartObject();
                jsonWriter.WrtiteNewPostEvent();
				jsonWriter.WriteEndObject();
                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();
            }
            return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(ms.ToArray())).RootElement;
        }

        private JsonElement GenerateNewPostEvent()
        {
            using var ms = new System.IO.MemoryStream();
            using (var jsonWriter = new Utf8JsonWriter(ms))
            {
				jsonWriter.WriteStartObject();
                jsonWriter.WrtiteNewPostEvent();
				jsonWriter.WriteEndObject();
            }
            return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(ms.ToArray())).RootElement;
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
