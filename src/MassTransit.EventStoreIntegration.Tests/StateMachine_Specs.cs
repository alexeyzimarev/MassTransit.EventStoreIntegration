using System;
using System.Linq;
using System.Threading.Tasks;
using Automatonymous;
using Automatonymous.Testing;
using MassTransit.EventStoreIntegration.Saga;
using MassTransit.Testing;
using MassTransit.Util;
using Shouldly;
using Xunit;

namespace MassTransit.EventStoreIntegration.Tests
{
    public class StateMachine_Specs : IDisposable, IClassFixture<EventStoreFixture>
    {
        EventStoreFixture _fixture;
        InMemoryTestHarness _harness;
        StateMachineSagaTestHarness<Instance, TestStateMachine> _saga;
        Guid _sagaId;
        private EventStoreSagaRepository<Instance> _repository;
        private TestStateMachine _machine;

        public StateMachine_Specs(EventStoreFixture fixture)
        {
            _fixture = fixture;
            _sagaId = Guid.NewGuid();

            _harness = new InMemoryTestHarness();
            _repository = new EventStoreSagaRepository<Instance>(_fixture.Connection);
            _machine = new TestStateMachine();
            _saga = _harness.StateMachineSaga(_machine, _repository);

            TaskUtil.Await(StartHarness);
        }

        private async Task StartHarness()
        {
            await _harness.Start();
            await _harness.InputQueueSendEndpoint.Send(new ProcessStarted {CorrelationId = _sagaId});
        }

        public void Dispose() => TaskUtil.Await(_harness.Stop);

        [Fact]
        public void Should_have_been_started()
        {
            _saga.Sagas.ContainsInState(_sagaId, _machine.Running, _machine);
        }

        [Fact]
        public async Task Should_load_the_stream()
        {
            var instance = _saga.Created.Contains(_sagaId);

            var data = await _fixture.Connection.ReadEvents(instance.StreamName, 512);
            data.Item1.ShouldBe(2);
            data.Item2.First().ShouldBeOfType<ProcessStarted>();
        }

        class Instance : EventSourcedSagaInstance, SagaStateMachineInstance
        {
            public Instance(Guid correlationId) : this()
            {
                CorrelationId = correlationId;
            }

            private Instance()
            {
                Register<SomeStringAssigned>(x => SomeString = x.NewValue);
            }

            public string SomeString { get; private set; }
        }

        class TestStateMachine : MassTransitStateMachine<Instance>
        {
            public TestStateMachine()
            {
                InstanceState(x => x.CurrentState);

                Event(() => Started);
                Event(() => Stopped);

                Initially(
                    When(Started)
                        .TransitionTo(Running));

                During(Running,
                    When(DataChanged)
                        .Then(c => c.Instance.Apply(c.Data)),
                    When(Stopped)
                        .TransitionTo(Done));
            }

            public State Running { get; private set; }
            public State Done { get; private set; }
            public Event<ProcessStarted> Started { get; private set; }
            public Event<ProcessStopped> Stopped { get; private set; }
            public Event<SomeStringAssigned> DataChanged { get; private set; }
        }

        class ProcessStarted : CorrelatedBy<Guid>
        {
            public Guid CorrelationId { get; set; }
        }

        class ProcessStopped : CorrelatedBy<Guid>
        {
            public Guid CorrelationId { get; set; }
        }

        class SomeStringAssigned : CorrelatedBy<Guid>
        {
            public Guid CorrelationId { get; set; }
            public string NewValue { get; set; }
        }
    }
}