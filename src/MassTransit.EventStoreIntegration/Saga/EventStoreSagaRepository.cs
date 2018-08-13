using System;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using GreenPipes;
using MassTransit.Logging;
using MassTransit.Saga;
using MassTransit.Util;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventStoreSagaRepository<TSaga> : ISagaRepository<TSaga>,
        IRetrieveSagaFromRepository<TSaga>
        where TSaga : class, IEventSourcedSaga
    {
        static readonly ILog Log = Logger.Get<EventStoreSagaRepository<TSaga>>();
        readonly IEventStoreConnection _connection;

        public EventStoreSagaRepository(IEventStoreConnection connection)
        {
            _connection = connection;
            if (TypeMapping.GetTypeName(typeof(EventSourcedSagaInstance.SagaInstanceTransitioned)).Contains("+"))
                TypeMapping.Add<EventSourcedSagaInstance.SagaInstanceTransitioned>("SagaInstanceTransitioned");
        }

        public async Task<TSaga> GetSaga(Guid correlationId)
        {
            var streamName = StreamName(correlationId);
            var data = await _connection.ReadEvents(streamName, 512, typeof(TSaga).Assembly);
            if (data == null) return null;

            var saga = SagaFactory();
            saga.Initialize(data.Events);
            saga.CorrelationId = correlationId;
            saga.ExpectedVersion = data.LastVersion;
            return saga;
        }

        private static string StreamName(Guid correlationId) =>
            TypeMapping.GetTypeName(typeof(TSaga)) + "-" + correlationId.ToString("N");

        public async Task Send<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            if (!context.CorrelationId.HasValue)
                throw new SagaException("The CorrelationId was not specified", typeof(TSaga), typeof(T));

            var sagaId = context.CorrelationId.Value;

            if (policy.PreInsertInstance(context, out var instance))
                await PreInsertSagaInstance<T>(instance, context.MessageId).ConfigureAwait(false);

            if (instance == null)
                instance = await GetSaga(sagaId);

            if (instance == null)
            {
                var missingSagaPipe = new MissingPipe<T>(_connection, next);
                await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
            }
            else
            {
                await SendToInstance(context, policy, next, instance).ConfigureAwait(false);
            }
        }

        public Task SendQuery<T>(SagaQueryConsumeContext<TSaga, T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            throw new NotImplementedByDesignException("Redis saga repository does not support queries");
        }

        public void Probe(ProbeContext context)
        {
            var scope = context.CreateScope("sagaRepository");
            scope.Set(new
            {
                Persistence = "connection"
            });
        }

        async Task SendToInstance<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next, TSaga instance)
            where T : class
        {
            try
            {
                if (Log.IsDebugEnabled)
                    Log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId, TypeMetadataCache<T>.ShortName);

                var sagaConsumeContext = new EventStoreSagaConsumeContext<TSaga, T>(_connection, context, instance);

                await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);

                if (!sagaConsumeContext.IsCompleted)
                    await _connection.SaveEvents(
                        instance.StreamName,
                        instance.GetChanges(),
                        instance.ExpectedVersion,
                        new EventMetadata{ CorrelationId = instance.CorrelationId, CausationId = context.MessageId});
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

        async Task<bool> PreInsertSagaInstance<T>(TSaga instance, Guid? causationId)
        {
            try
            {
                await _connection.SaveEvents(
                    instance.StreamName,
                    instance.GetChanges(),
                    instance.ExpectedVersion,
                    new EventMetadata{CorrelationId = instance.CorrelationId, CausationId = causationId});

                if (Log.IsDebugEnabled)
                    Log.DebugFormat("SAGA:{0}:{1} Insert {2}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                if (Log.IsDebugEnabled)
                    Log.DebugFormat("SAGA:{0}:{1} Dupe {2} - {3}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName, ex.Message);

                return false;
            }
        }

        private static TSaga SagaFactory()
        {
            var ctor = typeof(TSaga).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[0], null);
            return (TSaga) ctor.Invoke(new object[0]);
        }

        /// <summary>
        ///     Once the message pipe has processed the saga instance, add it to the saga repository
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        class MissingPipe<TMessage> :
            IPipe<SagaConsumeContext<TSaga, TMessage>>
            where TMessage : class
        {
            readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;
            readonly IEventStoreConnection _connection;

            public MissingPipe(IEventStoreConnection connection, IPipe<SagaConsumeContext<TSaga, TMessage>> next)
            {
                _connection = connection;
                _next = next;
            }

            void IProbeSite.Probe(ProbeContext context)
            {
                _next.Probe(context);
            }

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                var instance = context.Saga;

                if (Log.IsDebugEnabled)
                    Log.DebugFormat("SAGA:{0}:{1} Added {2}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId,
                        TypeMetadataCache<TMessage>.ShortName);

                SagaConsumeContext<TSaga, TMessage> proxy =
                    new EventStoreSagaConsumeContext<TSaga, TMessage>(_connection, context, instance);

                await _next.Send(proxy).ConfigureAwait(false);

                if (!proxy.IsCompleted)
                    await _connection.SaveEvents(
                        instance.StreamName,
                        instance.GetChanges(),
                        instance.ExpectedVersion,
                        new EventMetadata {CorrelationId = instance.CorrelationId, CausationId = context.MessageId});
            }
        }
    }
}