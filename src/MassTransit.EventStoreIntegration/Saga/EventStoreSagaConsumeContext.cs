using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using MassTransit.Context;
using MassTransit.Logging;
using MassTransit.Util;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventStoreSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextProxyScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, IEventSourcedSaga
    {
        static readonly ILog Log = Logger.Get<EventStoreSagaRepository<TSaga>>();
        readonly IEventStoreConnection _connection;

        public EventStoreSagaConsumeContext(IEventStoreConnection connection, ConsumeContext<TMessage> context,
            TSaga instance) : base(context)
        {
            Saga = instance;
            _connection = connection;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        SagaConsumeContext<TSaga, T> SagaConsumeContext<TSaga>.PopContext<T>()
        {
            var context = this as SagaConsumeContext<TSaga, T>;
            if (context == null)
                throw new ContextException(
                    $"The ConsumeContext<{TypeMetadataCache<TMessage>.ShortName}> could not be cast to {TypeMetadataCache<T>.ShortName}");

            return context;
        }

        async Task SagaConsumeContext<TSaga>.SetCompleted()
        {
            await _connection.DeleteStreamAsync(Saga.StreamName, Saga.ExpectedVersion, false);

            IsCompleted = true;
            if (Log.IsDebugEnabled)
                Log.DebugFormat("SAGA:{0}:{1} Removed {2}", TypeMetadataCache<TSaga>.ShortName,
                    TypeMetadataCache<TMessage>.ShortName,
                    Saga.CorrelationId);
        }

        public TSaga Saga { get; }
        public bool IsCompleted { get; private set; }
    }
}