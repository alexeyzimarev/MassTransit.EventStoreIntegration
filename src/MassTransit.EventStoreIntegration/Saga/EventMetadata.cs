using System;
using Newtonsoft.Json;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventMetadata
    {
        [JsonProperty("$correlationId")]
        public Guid CorrelationId { get; set; }

        [JsonProperty("$causationId")]
        public Guid? CausationId { get; set; }
    }
}