using System.Text.Json;

namespace SNPN.Monitor;

public class EventParser
{
    public EventType GetEventType(JsonElement eventJson)
    {
        var eventType = eventJson.GetProperty("eventType").GetString()?.ToLower();
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

    public long GetLatestEventId(JsonElement eventsJson)
    {
        long eventId = 0;
        if (eventsJson.TryGetProperty("lastEventId", out var eventIdProperty))
        {
            eventId = eventIdProperty.GetInt64();
        }
        return eventId;
    }

    public NewPostEvent GetNewPostEvent(JsonElement eventJson)
    {
        if (GetEventType(eventJson) != EventType.NewPost)
        {
            throw new ArgumentException("Wrong event type.", nameof(eventJson));
        }
        var newEvent = eventJson.GetProperty("eventData").Deserialize<NewPostEvent>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return newEvent;
    }

    public IEnumerable<NewPostEvent> GetNewPostEvents(JsonElement eventsJson)
    {
        var events = new List<NewPostEvent>();
        if (eventsJson.TryGetProperty("events", out var eventsProperty))
        {
            foreach (var e in eventsProperty.EnumerateArray()) //PERF: Could probably Parallel.ForEach this.
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
