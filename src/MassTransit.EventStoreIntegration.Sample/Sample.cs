using System;
using System.Configuration;
using System.Threading.Tasks;
using Automatonymous;
using EventStore.ClientAPI;
using EventStore.SerilogAdapter;
using GreenPipes;
using MassTransit.EventStoreIntegration.Saga;
using Serilog;

namespace MassTransit.EventStoreIntegration.Sample
{
    public class Sample
    {
        private IBusControl _bus;
        private IEventStoreConnection _connection;

        public Sample()
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.LiterateConsole().CreateLogger();

            var connectionString = ConfigurationManager.ConnectionStrings["eventStore"];
            _connection = EventStoreConnection.Create(connectionString.ConnectionString,
                ConnectionSettings.Create().UseSerilog());

            var repository = new EventStoreSagaRepository<SampleInstance>(_connection);
            _bus = Bus.Factory.CreateUsingRabbitMq(c =>
            {
                c.UseSerilog();
                c.UseConcurrencyLimit(1);
                c.PrefetchCount = 1;

                var host = c.Host(new Uri("rabbitmq://localhost"), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                var machine = new SampleStateMachine();
                c.ReceiveEndpoint(host, "essaga_test", ep => ep.StateMachineSaga(machine, repository));
            });
        }

        public async Task Execute()
        {
            await _connection.ConnectAsync();
            await _bus.StartAsync();
            var endpoint = await _bus.GetSendEndpoint(new Uri("rabbitmq://localhost/essaga_test"));

            var sagaId = Guid.NewGuid();
            await endpoint.Send(new ProcessStarted {CorrelationId = sagaId, OrderId = "321"});

            await endpoint.Send(new OrderStatusChanged {CorrelationId = sagaId, OrderStatus = "Pending"});

            await endpoint.Send(new ProcessStopped {CorrelationId = sagaId});
        }

        public void Stop()
        {
            _bus.Stop();
            _connection.Dispose();
        }
    }
}