using System.Collections.Generic;

namespace MassTransit.EventStoreIntegration
{
    public class EventsData
    {
        public EventsData(IEnumerable<object> events, long lastVersion)
        {
            Events = events;
            LastVersion = lastVersion;
        }

        public IEnumerable<object> Events { get; }
        public long LastVersion { get; }
    }
}