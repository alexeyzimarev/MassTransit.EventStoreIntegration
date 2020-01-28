using System;

namespace MassTransit.EventStoreIntegration.Sample
{
    public class ProcessStarted : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
        public string OrderId { get; set; }
    }

    public class ProcessStopped : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
    }

    public class OrderStatusChanged : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
        public string OrderStatus { get; set; }
    }
}