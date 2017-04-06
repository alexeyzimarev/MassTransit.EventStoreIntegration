using System;
using MassTransit.Saga;

namespace MassTransit.EventStoreIntegration.Saga
{
    public interface IRetrieveSagaFromRepository<out TSaga> where TSaga: ISaga
    {
        TSaga GetSaga(Guid correlationId);
    }
}
