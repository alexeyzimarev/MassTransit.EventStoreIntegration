using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Automatonymous;
using MassTransit.EventStoreIntegration.Saga;
using MassTransit.Testing;
using Shouldly;
using Xunit;

namespace MassTransit.EventStoreIntegration.Tests
{
    public class StateMachine_Specs : IAsyncLifetime
    {
        InMemoryTestHarness                                     _harness;
        StateMachineSagaTestHarness<Instance, TestStateMachine> _saga;
        Guid                                                    _sagaId;
        EventStoreSagaRepository<Instance>                      _repository;
        TestStateMachine                                        _machine;

        public StateMachine_Specs()
        {
            _sagaId = Guid.NewGuid();

            _harness    = new InMemoryTestHarness();
            _repository = new EventStoreSagaRepository<Instance>(EventStoreFixture.Connection);
            _machine    = new TestStateMachine();
            _saga       = _harness.StateMachineSaga(_machine, _repository);
        }

        public TimeSpan TestTimeout =>
            Debugger.IsAttached ? TimeSpan.FromMinutes(50) : TimeSpan.FromSeconds(30);

        static string StreamName<T>(Guid guid) =>
            TypeMapping.GetTypeName(typeof(T)) + "-" + guid.ToString("N");

        [Fact]
        public async Task Should_have_been_started()
        {
            var instance = await _repository.ShouldContainSaga(_sagaId, TestTimeout);
            instance.ShouldNotBeNull();
        }

        [Fact]
        public async Task Should_load_the_stream()
        {
            var instance = await _repository.ShouldContainSaga(_sagaId, TestTimeout);

            var streamName = StreamName<Instance>(_sagaId);
            var events =
                await EventStoreFixture.Connection.ReadEvents(streamName, 512,
                    Assembly.GetExecutingAssembly().GetName().Name);
            events.LastVersion.ShouldBe(2);
            events.Events.ElementAt(1).ShouldBeOfType<ProcessStarted>();
        }

//        [Fact]
        public async Task Should_assign_value()
        {
            await _repository.ShouldContainSaga(_sagaId, TestTimeout);
            await _harness.InputQueueSendEndpoint.Send(
                new SomeStringAssigned {CorrelationId = _sagaId, NewValue = "new"});
            await _repository.ShouldContainSaga(_sagaId, x => x.SomeString == "new", TestTimeout);

            var streamName = StreamName<Instance>(_sagaId);
            var events =
                await EventStoreFixture.Connection.ReadEvents(streamName, 512,
                    Assembly.GetExecutingAssembly().GetName().Name);
            events.LastVersion.ShouldBe(2);
            events.Events.ElementAt(1).ShouldBeOfType<ProcessStarted>();
        }

        class Instance : EventSourcedSagaInstance, SagaStateMachineInstance
        {
            public Instance(Guid correlationId) : this()
            {
                CorrelationId = correlationId;
            }

            Instance()
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

                Event(() => Started,
                    x => x.CorrelateById(e => e.Message.CorrelationId).SelectId(e => e.Message.CorrelationId));
                Event(() => Stopped, x => x.CorrelateById(e => e.Message.CorrelationId));
                Event(() => DataChanged, x => x.CorrelateById(e => e.Message.CorrelationId));

                Initially(
                    When(Started)
                        .Then(c => c.Instance.Apply(c.Data))
                        .TransitionTo(Running));

                During(Running,
                    When(DataChanged)
                        .Then(c => c.Instance.Apply(c.Data)),
                    When(Stopped)
                        .TransitionTo(Done));
            }

            public State                     Running     { get; private set; }
            public State                     Done        { get; private set; }
            public Event<ProcessStarted>     Started     { get; private set; }
            public Event<ProcessStopped>     Stopped     { get; private set; }
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
            public Guid   CorrelationId { get; set; }
            public string NewValue      { get; set; }
        }

        public async Task InitializeAsync()
        {
            await _harness.Start();
            await _harness.InputQueueSendEndpoint.Send(new ProcessStarted {CorrelationId = _sagaId});
        }

        public Task DisposeAsync() => _harness.Stop();
    }
}