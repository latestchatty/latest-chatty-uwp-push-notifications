using Newtonsoft.Json.Linq;
using SNPN.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SNPN.Monitor
{
	public class EventParser
	{
		public EventType GetEventType(JToken eventJson)
		{
			var eventType = eventJson["eventType"]?.Value<string>().ToLower();
			switch (eventType)
			{
				case "categorychange":
					return EventType.CategoryChange;
				case "lolcountsupdate":
					return EventType.LolCountsUpdate;
				case "newpost":
					return EventType.NewPost;
				case "servermessage":
					return EventType.ServerMessage;
				default:
					break;
			}
			return EventType.Uknown;
		}

		public long GetLatestEventId(JToken eventsJson)
		{
			if (eventsJson["lastEventId"] != null)
			{
				return (long)eventsJson["lastEventId"];
			}
			return 0;
		}

		public NewPostEvent GetNewPostEvent(JToken eventJson)
		{
			if (this.GetEventType(eventJson) != EventType.NewPost)
			{
				throw new ArgumentException("Wrong event type.", nameof(eventJson));
			}
			var newEvent = eventJson["eventData"].ToObject<NewPostEvent>();
			return newEvent;
		}

		public IEnumerable<NewPostEvent> GetNewPostEvents(JToken eventsJson)
		{
			var events = new List<NewPostEvent>();
			if (eventsJson["events"] != null)
			{
				foreach (var e in eventsJson["events"]) //PERF: Could probably Parallel.ForEach this.
				{
					var eventType = this.GetEventType(e);
					if (eventType == EventType.NewPost)
					{
						var parsedNewPost = this.GetNewPostEvent(e);
						events.Add(parsedNewPost);
					}
				}
			}
			return events.AsEnumerable();
		}
	}
}
