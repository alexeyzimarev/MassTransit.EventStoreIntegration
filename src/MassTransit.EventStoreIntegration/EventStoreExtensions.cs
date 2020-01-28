using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using MassTransit.EventStoreIntegration.Saga;

namespace MassTransit.EventStoreIntegration
{
    public static class EventStoreExtensions
    {
        /// <summary>
        /// Serialize and append events to the specified stream
        /// </summary>
        /// <param name="connection">EventStore connection</param>
        /// <param name="stream">Stream name</param>
        /// <param name="events">Collection of events</param>
        /// <param name="expectedVersion">Expected stream version</param>
        /// <returns></returns>
        public static async Task<long> SaveEvents(
            this IEventStoreConnection connection,
            string stream,
            IEnumerable<(object @event, EventMetadata metadata)> events,
            long expectedVersion
        )
        {
            var esEvents = events
                .Select(x =>
                    new EventData(
                        Guid.NewGuid(),
                        TypeMapping.GetTypeName(x.@event.GetType()),
                        true,
                        JsonSerialisation.Serialize(x.@event),
                        JsonSerialisation.Serialize(x.metadata)
                    )
                );

            var result = await connection.AppendToStreamAsync(stream, expectedVersion, esEvents).ConfigureAwait(false);
            
            return result.NextExpectedVersion;
        }

        public static async Task<EventsData> ReadEvents(
            this IEventStoreConnection connection,
            string streamName, int sliceSize, string assemblyName
        )
        {
            var slice = await
                connection.ReadStreamEventsForwardAsync(streamName, StreamPosition.Start, sliceSize, false);
            if (slice.Status == SliceReadStatus.StreamDeleted || slice.Status == SliceReadStatus.StreamNotFound)
                return null;

            var lastEventNumber = slice.LastEventNumber;
            var events          = new List<object>();
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