using System;
using System.Collections.Generic;

namespace MassTransit.EventStoreIntegration.Saga
{
    public abstract class EventSourcedSaga : IEventSourcedSaga
    {
        public Guid CorrelationId { get; set; }
        public int ExpectedVersion { get; private set; }

        public void Initialize(IEnumerable<object> events)
        {
            throw new NotImplementedException();
        }

        public bool HasChanges()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetChanges()
        {
            throw new NotImplementedException();
        }

        public void ClearChanges()
        {
            throw new NotImplementedException();
        }
    }
}