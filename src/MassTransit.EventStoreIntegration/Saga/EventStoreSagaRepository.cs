using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using GreenPipes;
using MassTransit.Saga;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventStoreSagaRepository<TSaga> : ISagaRepository<TSaga>,
        IRetrieveSagaFromRepository<TSaga>
        where TSaga : class, IEventSourcedSaga
    {
        readonly IEventStoreConnection   _connection;
        readonly GetEventMetadata<TSaga> _getEventMetadata;
        readonly string                  _assemblyName;

        public EventStoreSagaRepository(
            IEventStoreConnection connection, GetEventMetadata<TSaga> getEventMetadata = null
        )
        {
            _connection       = connection;
            _getEventMetadata = getEventMetadata ?? DefaultMetadataFactory;
            _assemblyName     = typeof(TSaga).Assembly.GetName().Name;

            if (TypeMapping.GetTypeName(typeof(EventSourcedSagaInstance.SagaInstanceTransitioned)).Contains("+"))
                TypeMapping.Add<EventSourcedSagaInstance.SagaInstanceTransitioned>("SagaInstanceTransitioned");
        }

        public async Task<TSaga> GetSaga(Guid correlationId)
        {
            var streamName = StreamName(correlationId);
            var data       = await _connection.ReadEvents(streamName, 512, _assemblyName);
            if (data == null) return null;

            var saga = SagaFactory();
            saga.Initialize(data.Events);
            saga.CorrelationId   = correlationId;
            saga.ExpectedVersion = data.LastVersion;
            return saga;
        }

        static string StreamName(Guid correlationId) =>
            TypeMapping.GetTypeName(typeof(TSaga)) + "-" + correlationId.ToString("N");

        public async Task Send<T>(
            ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next
        ) where T : class
        {
            if (!context.CorrelationId.HasValue)
                throw new SagaException("The CorrelationId was not specified", typeof(TSaga), typeof(T));

            var sagaId = context.CorrelationId.Value;

            if (policy.PreInsertInstance(context, out var instance))
                await PreInsertSagaInstance(instance, context).ConfigureAwait(false);

            if (instance == null)
                instance = await GetSaga(sagaId).ConfigureAwait(false);

            if (instance == null)
            {
                var missingSagaPipe = new MissingPipe<T>(_connection, next, _getEventMetadata);
                await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
            }
            else
            {
                await SendToInstance(context, policy, next, instance).ConfigureAwait(false);
            }
        }

        public Task SendQuery<T>(
            ConsumeContext<T> context, ISagaQuery<TSaga> query, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next
            ) where T : class
        {
            throw new NotImplementedByDesignException("EventStore saga repository does not support queries");
        }

        public void Probe(ProbeContext context)
        {
            var scope = context.CreateScope("sagaRepository");
            scope.Set(new {Persistence = "connection"});
        }

        async Task SendToInstance<T>(
            ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next, TSaga instance
        )
            where T : class
        {
            try
            {
                var sagaConsumeContext = new EventStoreSagaConsumeContext<TSaga, T>(_connection, context, instance);
                
                sagaConsumeContext.LogUsed();

                await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);

                if (!sagaConsumeContext.IsCompleted)
                    await _connection.PersistSagaInstance(instance, context, _getEventMetadata);
            }
            catch (SagaException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SagaException(ex.Message, typeof(TSaga), typeof(T), instance.CorrelationId, ex);
            }
        }

        async Task PreInsertSagaInstance<T>(TSaga instance, ConsumeContext<T> context) where T : class 
        {
            try
            {
                await _connection.PersistSagaInstance(instance, context, _getEventMetadata).ConfigureAwait(false);

                context.LogInsert<TSaga, T>(instance.CorrelationId);
            }
            catch (Exception ex)
            {
                context.LogInsertFault<TSaga, T>(ex, instance.CorrelationId);
            }
        }

        static TSaga SagaFactory()
        {
            var ctor = typeof(TSaga).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[0], null);
            return (TSaga) ctor.Invoke(new object[0]);
        }

        static EventMetadata DefaultMetadataFactory(TSaga saga, ConsumeContext context, object @event)
            =>
                new EventMetadata(
                    (EventMetadataKeys.CorrelationId, saga.CorrelationId.ToString()),
                    (EventMetadataKeys.CausationId, context.MessageId.ToString())
                );

        /// <summary>
        ///     Once the message pipe has processed the saga instance, add it to the saga repository
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        class MissingPipe<TMessage> :
            IPipe<SagaConsumeContext<TSaga, TMessage>>
            where TMessage : class
        {
            readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;
            readonly GetEventMetadata<TSaga>                    _metadataFactory;
            readonly IEventStoreConnection                      _connection;

            public MissingPipe(
                IEventStoreConnection connection, IPipe<SagaConsumeContext<TSaga, TMessage>> next,
                GetEventMetadata<TSaga> metadataFactory
            )
            {
                _connection      = connection;
                _next            = next;
                _metadataFactory = metadataFactory;
            }

            void IProbeSite.Probe(ProbeContext context) => _next.Probe(context);

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                var instance = context.Saga;

                SagaConsumeContext<TSaga, TMessage> proxy =
                    new EventStoreSagaConsumeContext<TSaga, TMessage>(_connection, context, instance);
                
                proxy.LogAdded();

                await _next.Send(proxy).ConfigureAwait(false);

                if (!proxy.IsCompleted)
                    await _connection.PersistSagaInstance(instance, context, _metadataFactory).ConfigureAwait(false);
            }
        }
    }

    static class EsConnectionExtensions
    {
        public static Task PersistSagaInstance<T>(
            this IEventStoreConnection connection,
            T saga, ConsumeContext context, GetEventMetadata<T> getEventMetadata
        )
            where T : class, IEventSourcedSaga
            => connection.SaveEvents(
                saga.StreamName,
                saga.GetChanges().Select(x => (x, getEventMetadata(saga, context, x))),
                saga.ExpectedVersion
            );
    }

    public delegate EventMetadata GetEventMetadata<in TSaga>(TSaga saga, ConsumeContext context, object @event)
        where TSaga : class, ISaga;
}