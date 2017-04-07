using System;
using System.Threading.Tasks;

namespace MassTransit.EventStoreIntegration.Saga
{
    public interface IRetrieveSagaFromRepository<TSaga> where TSaga: IEventSourcedSaga
    {
        Task<TSaga> GetSaga(Guid correlationId);
    }
}
