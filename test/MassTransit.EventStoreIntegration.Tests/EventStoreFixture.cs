using System;
using System.IO;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Microsoft.Extensions.Configuration;
using static System.TimeSpan;

namespace MassTransit.EventStoreIntegration.Tests
{
    public static class EventStoreFixture
    {
        public static IEventStoreConnection Connection { get; }

        static EventStoreFixture()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, false)
                .AddJsonFile(
                    $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json".ToLower(), true)
                .AddEnvironmentVariables()
                .Build();

            Connection = ConfigureEventStore(Configuration["EventStore:ConnectionString"]).GetAwaiter().GetResult();
        }

        static IConfiguration Configuration { get; }

        static async Task<IEventStoreConnection> ConfigureEventStore(string connectionString)
        {
            var gesConnection = EventStoreConnection.Create(
                connectionString,
                ConnectionSettings.Create()
                    .EnableVerboseLogging()
                    .KeepReconnecting()
                    .KeepRetrying()
                    .SetOperationTimeoutTo(FromSeconds(10))
                    .SetTimeoutCheckPeriodTo(FromSeconds(2))
                    .SetReconnectionDelayTo(FromSeconds(3))
                    .SetHeartbeatTimeout(FromSeconds(6))
                    .SetHeartbeatInterval(FromSeconds(3))
                    .SetGossipTimeout(FromSeconds(2)),
                "DCore.Platform.Integration.EventStore.Tests"
            );

            await gesConnection.ConnectAsync();

            return gesConnection;
        }
    }
}