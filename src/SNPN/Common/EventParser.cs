using Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SNPN.Common
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

		public NewPostEvent GetNewPostEvent(JToken eventJson)
		{
			if (this.GetEventType(eventJson) != EventType.NewPost)
			{
				throw new ArgumentException("Wrong event type.", nameof(eventJson));
			}
			var newEvent = eventJson["eventData"].ToObject<NewPostEvent>();
			return newEvent;
		}
	}
}
