using System;
using System.Linq;
using System.Collections.Generic;

namespace MassTransit.EventStoreIntegration.Saga
{
    public abstract class EventSourcedSagaInstance : IEventSourcedSaga
    {
        public Guid CorrelationId { get; set; }
        public long ExpectedVersion { get; set; }

        public string StreamName =>
            TypeMapping.GetTypeName(GetType()) + "-" + CorrelationId.ToString("N");

        private string _currentState;

        public string CurrentState
        {
            get => _currentState;
            set => Apply(new SagaInstanceTransitioned
            {
                InstanceId = CorrelationId,
                NewState = value
            });
        }

        readonly EventRecorder _recorder;
        readonly EventRouter _router;

        protected EventSourcedSagaInstance()
        {
            _router = new EventRouter();
            _recorder = new EventRecorder();
            ExpectedVersion = EventStore.ClientAPI.ExpectedVersion.NoStream;
            Register<SagaInstanceTransitioned>(x => _currentState = x.NewState);
        }

        /// <summary>
        /// Registers the state handler to be invoked when the specified event is applied.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event to register the handler for.</typeparam>
        /// <param name="handler">The handler.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="handler"/> is null.</exception>
        protected void Register<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _router.ConfigureRoute(handler);
        }

        /// <summary>
        /// Applies the specified event to this instance and invokes the associated state handler.
        /// </summary>
        /// <param name="event">The event to apply.</param>
        public void Apply(object @event)
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));
            Play(@event);
            Record(@event);
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes this instance using the specified events.
        /// </summary>
        /// <param name="events">The events to initialize with.</param>
        /// <exception cref="T:System.ArgumentNullException">Thrown when the <paramref name="events" /> are null.</exception>
        public void Initialize(IEnumerable<object> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (HasChanges())
                throw new InvalidOperationException("Initialize cannot be called on an instance with changes.");

            foreach (var @event in events)
            {
                Play(@event);
            }
        }

        /// <summary>
        /// Determines whether this instance has state changes.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance has state changes; otherwise, <c>false</c>.
        /// </returns>
        public bool HasChanges() => _recorder.Any();

        /// <summary>
        /// Gets the state changes applied to this instance.
        /// </summary>
        /// <returns>A list of recorded state changes.</returns>
        public IEnumerable<object> GetChanges() => _recorder.ToArray();

        /// <summary>
        /// Clears the state changes.
        /// </summary>
        public void ClearChanges() => _recorder.Reset();

        void Play(object @event) => _router.Route(@event);

        void Record(object @event) => _recorder.Record(@event);

        public class SagaInstanceTransitioned
        {
            public Guid InstanceId { get; set; }
            public string NewState { get; set; }
        }
    }
}