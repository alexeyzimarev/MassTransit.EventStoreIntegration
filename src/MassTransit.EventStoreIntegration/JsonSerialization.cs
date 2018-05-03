using System.Collections.Generic;
using System.IO;
using EventStore.ClientAPI;
using Newtonsoft.Json;

namespace MassTransit.EventStoreIntegration
{
    public static class JsonSerialisation
    {
        public static IEnumerable<object> Deserialize(ResolvedEvent resolvedEvent, string assemblyName)
        {
            var type = TypeMapping.Get(resolvedEvent.Event.EventType, assemblyName);
            if (type == null)
            {
                yield return null;
                yield break;
            }

            using (var stream = new MemoryStream(resolvedEvent.Event.Data))
            using (var reader = new StreamReader(stream))
            {
                yield return JsonSerializer.CreateDefault().Deserialize(reader, type);
            }
        }

        public static byte[] Serialize(object @event)
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
