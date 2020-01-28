using System;
using System.IO;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using MassTransit.Audit;
using MassTransit.Metadata;
using Newtonsoft.Json;

namespace MassTransit.EventStoreIntegration.Audit
{
    public class EventStoreMessageAudit : IMessageAuditStore
    {
        readonly IEventStoreConnection _connection;
        readonly string _auditStreamName;

        public EventStoreMessageAudit(IEventStoreConnection connection, string auditStreamName)
        {
            _connection = connection;
            _auditStreamName = auditStreamName;
        }

        public Task StoreMessage<T>(T message, MessageAuditMetadata metadata) where T : class
        {
            var auditEvent = new EventData(Guid.NewGuid(), TypeMetadataCache<T>.ShortName,
                true, Serialise(message), Serialise(metadata));
            return _connection.AppendToStreamAsync(_auditStreamName, ExpectedVersion.Any, auditEvent);
        }

        static byte[] Serialise(object @event)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            
            JsonSerializer.CreateDefault().Serialize(writer, @event);
            writer.Flush();
            
            return stream.ToArray();
        }
    }
}