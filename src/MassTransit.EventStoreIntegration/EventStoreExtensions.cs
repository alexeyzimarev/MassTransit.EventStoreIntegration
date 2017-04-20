using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace MassTransit.EventStoreIntegration
{
    public static class EventStoreExtensions
    {
        public static async Task<int> SaveEvents(this IEventStoreConnection connection,
            string streamIdentifier,
            IEnumerable<object> events,
            int expectedVersion = ExpectedVersion.Any,
            object metadata = null)
        {
            var esEvents = events
                .Select(x =>
                    new EventData(
                        Guid.NewGuid(),
                        TypeMapping.GetTypeName(x.GetType()),
                        true,
                        JsonSerialisation.Serialize(x),
                        JsonSerialisation.Serialize(metadata)));

            var result = await connection.AppendToStreamAsync(streamIdentifier, expectedVersion, esEvents);
            return result.NextExpectedVersion;
        }

        public static async Task<EventsData> ReadEvents(this IEventStoreConnection connection,
            string streamName, int sliceSize, Assembly assembly)
        {
            var slice = await
                connection.ReadStreamEventsForwardAsync(streamName, StreamPosition.Start, sliceSize, false);
            if (slice.Status == SliceReadStatus.StreamDeleted || slice.Status == SliceReadStatus.StreamNotFound)
                return null;

            var assemblyName = assembly.GetName().Name;
            var lastEventNumber = slice.LastEventNumber;
            var events = new List<object>();
            events.AddRange(slice.Events.SelectMany(x => JsonSerialisation.Deserialize(x, assemblyName)));

            while (!slice.IsEndOfStream)
            {
                slice = await
                    connection.ReadStreamEventsForwardAsync(streamName, slice.NextEventNumber, sliceSize, false);
                events.AddRange(slice.Events.SelectMany(x => JsonSerialisation.Deserialize(x, assemblyName)));
                lastEventNumber = slice.LastEventNumber;
            }
            var tuple = new EventsData(events, lastEventNumber);
            return tuple;
        }
    }
}