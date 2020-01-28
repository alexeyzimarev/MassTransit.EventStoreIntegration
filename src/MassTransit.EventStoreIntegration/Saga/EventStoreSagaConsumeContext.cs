using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using MassTransit.Context;
using MassTransit.Saga;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventStoreSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, IEventSourcedSaga
    {
        readonly IEventStoreConnection _connection;

        public EventStoreSagaConsumeContext(IEventStoreConnection connection, ConsumeContext<TMessage> context,
            TSaga instance) : base(context)
        {
            Saga = instance;
            _connection = connection;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        async Task SagaConsumeContext<TSaga>.SetCompleted()
        {
            await _connection.DeleteStreamAsync(Saga.StreamName, Saga.ExpectedVersion, false);

            IsCompleted = true;
            
            this.LogRemoved();
        }

        public TSaga Saga { get; }
        public bool IsCompleted { get; private set; }
    }
}