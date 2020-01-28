using System;
using Automatonymous;

namespace MassTransit.EventStoreIntegration.Sample
{
    public class SampleStateMachine : MassTransitStateMachine<SampleInstance>
    {
        public SampleStateMachine()
        {
            InstanceState(x => x.CurrentState);

            Event(() => Started,
                x => x.CorrelateById(e => e.Message.CorrelationId).SelectId(e => e.Message.CorrelationId));
            Event(() => Stopped, x => x.CorrelateById(e => e.Message.CorrelationId));
            Event(() => StatusChanged, x => x.CorrelateById(e => e.Message.CorrelationId));

            Initially(
                When(Started)
                    .Then(c => c.Instance.Apply(c.Data))
                    .TransitionTo(Running));

            During(Running,
                When(StatusChanged)
                    .Then(c => c.Instance.Apply(c.Data)),
                When(Stopped)
                    .Then(c => Console.WriteLine($"Current state: {c.Instance}"))
                    .TransitionTo(Done)
                    .Finalize());
        }

        public State Running { get; private set; }
        public State Done { get; private set; }
        public Event<ProcessStarted> Started { get; private set; }
        public Event<ProcessStopped> Stopped { get; private set; }
        public Event<OrderStatusChanged> StatusChanged { get; private set; }
    }
}