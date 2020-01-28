using System;
using System.Collections.Generic;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventMetadata : Dictionary<string, string>
    {
        public EventMetadata(params (string key, string value)[] entries)
            => Array.ForEach(entries, entry => Add(entry.key, entry.value));
    }

    public static class EventMetadataKeys
    {
        public const string CorrelationId = "$correlationId";
        public const string CausationId   = "$causationId";
    }
}