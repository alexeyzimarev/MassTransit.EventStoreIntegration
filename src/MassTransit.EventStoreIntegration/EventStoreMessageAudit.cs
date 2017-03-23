using System;
using System.IO;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using MassTransit.Audit;
using MassTransit.Util;
using Newtonsoft.Json;

namespace MassTransit.EventStoreIntegration
{
    public class EventStoreMessageAudit : IMessageAuditStore
    {
        private readonly IEventStoreConnection _connection;
        private readonly string _auditStreamName;

        public EventStoreMessageAudit(IEventStoreConnection connection, string auditStreamName)
        {
            _connection = connection;
            _auditStreamName = auditStreamName;
        }

        public async Task StoreMessage<T>(T message, MessageAuditMetadata metadata) where T : class
        {
            var auditEvent = new EventData(Guid.NewGuid(), TypeMetadataCache<T>.ShortName,
                true, Serialise(message), Serialise(metadata));
            await _connection.AppendToStreamAsync(_auditStreamName, ExpectedVersion.Any, auditEvent)
                .ConfigureAwait(false);
        }

        private static byte[] Serialise(object @event)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    JsonSerializer.CreateDefault().Serialize(writer, @event);
                    writer.Flush();
                }
                return stream.ToArray();
            }
        }
    }
}