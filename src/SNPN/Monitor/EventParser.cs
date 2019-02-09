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
			}
			return EventType.Uknown;
		}

		public long GetLatestEventId(JToken eventsJson)
		{
			long eventId = 0;
			if (eventsJson["lastEventId"] != null)
			{
				eventId = (long)eventsJson["lastEventId"];
			}
			return eventId;
		}

		public NewPostEvent GetNewPostEvent(JToken eventJson)
		{
			if (GetEventType(eventJson) != EventType.NewPost)
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
					var eventType = GetEventType(e);
					if (eventType == EventType.NewPost)
					{
						var parsedNewPost = GetNewPostEvent(e);
						events.Add(parsedNewPost);
					}
				}
			}
			return events.AsEnumerable();
		}
	}
}
