using System;
using Automatonymous;
using MassTransit.EventStoreIntegration.Saga;

namespace MassTransit.EventStoreIntegration.Sample
{
    public class SampleInstance : EventSourcedSagaInstance, SagaStateMachineInstance
    {
        public SampleInstance(Guid correlationId) : this()
        {
            CorrelationId = correlationId;
        }

        private SampleInstance()
        {
            Register<ProcessStarted>(x => OrderId = x.OrderId);
            Register<OrderStatusChanged>(x => OrderStatus = x.OrderStatus);
        }

        public string OrderStatus { get; private set; }
        public string OrderId { get; private set; }
    }
}