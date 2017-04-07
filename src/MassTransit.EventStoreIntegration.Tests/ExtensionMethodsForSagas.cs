using System;
using System.Threading.Tasks;
using MassTransit.EventStoreIntegration.Saga;
using MassTransit.Saga;

namespace MassTransit.EventStoreIntegration.Tests
{
    public static class ExtensionMethodsForSagas
    {
        public static async Task<bool> ShouldContainSaga<TSaga>(this ISagaRepository<TSaga> repository, Guid sagaId,
            TimeSpan timeout) where TSaga : class, IEventSourcedSaga
        {
            var giveUpAt = DateTime.Now + timeout;

            while (DateTime.Now < giveUpAt)
            {
                var saga = await (repository as IRetrieveSagaFromRepository<TSaga>).GetSaga(sagaId);
                if (saga != null)
                    return true;
                Task.Delay(10);
            }

            return false;
        }

        public static async Task<bool> ShouldContainSaga<TSaga>(this ISagaRepository<TSaga> repository, Guid sagaId,
            Func<TSaga, bool> condition, TimeSpan timeout) where TSaga : class, IEventSourcedSaga
        {
            var giveUpAt = DateTime.Now + timeout;

            while (DateTime.Now < giveUpAt)
            {
                var saga = await (repository as IRetrieveSagaFromRepository<TSaga>).GetSaga(sagaId);
                if (condition(saga))
                    return true;
                await Task.Delay(10);
            }

            return false;
        }

        public static async Task<bool> ShouldNotContainSaga<TSaga>(this ISagaRepository<TSaga> repository, Guid sagaId,
            TimeSpan timeout) where TSaga : class, IEventSourcedSaga
        {
            var giveUpAt = DateTime.Now + timeout;

            while (DateTime.Now < giveUpAt)
            {
                var saga = await (repository as IRetrieveSagaFromRepository<TSaga>).GetSaga(sagaId);
                if (saga == null)
                    return true;

                Task.Delay(10);
            }

            return false;
        }
    }
}