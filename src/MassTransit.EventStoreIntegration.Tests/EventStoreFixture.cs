using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.ClientAPI.SystemData;
using EventStore.Core;
using EventStore.Core.Data;
using MassTransit.Util;
using Xunit;

namespace MassTransit.EventStoreIntegration.Tests
{
    public class EventStoreFixture : IDisposable
    {
        public ClusterVNode Node { get; }

        public IEventStoreConnection Connection { get; }

        public UserCredentials Credentials { get; }

        public EventStoreFixture()
        {
            var node = EmbeddedVNodeBuilder.
                AsSingleNode().
                OnDefaultEndpoints().
                RunInMemory().
                Build();

            var tcs = new TaskCompletionSource<object>();
            node.NodeStatusChanged += (sender, args) =>
            {
                if (args.NewVNodeState == VNodeState.Master)
                    tcs.SetResult(null);
            };
            node.Start();

            tcs.Task.Wait();
            Node = node;

            // This is a workaround for not authenticated error that happens sometimes (often on build server)
            // According to this: https://groups.google.com/forum/#!topic/event-store/1lAIj5ipDy0 the problem was previously identified and fixed
            // However despite us handling the NodeStatusChanged event, it still happens.
            // TODO: follow up on EventStore google group
            Thread.Sleep(2000);

            Credentials = new UserCredentials("admin", "changeit");
            var connection = EmbeddedEventStoreConnection.Create(Node);

            // This does not work, because ... ††† JEZUS †††
            //var connection = EventStoreConnection.Create(
            //    ConnectionSettings.Create().SetDefaultUserCredentials(Credentials).UseDebugLogger(),
            //    new IPEndPoint(Opts.InternalIpDefault, Opts.ExternalTcpPortDefault));
            TaskUtil.Await(() => connection.ConnectAsync());
            Connection = connection;
        }

        public void Dispose()
        {
            var connection = Connection;
            if (connection != null)
            {
                connection.Close();
                connection.Dispose();
            }
            var node = Node;
            if (node == null) return;
            var tcs = new TaskCompletionSource<object>();
            node.NodeStatusChanged += (sender, args) =>
            {
                if (args.NewVNodeState == VNodeState.Shutdown)
                    tcs.SetResult(null);
            };
            node.Stop();
            tcs.Task.Wait();
        }
    }

    [CollectionDefinition("EventStoreCollection")]
    public class EventStoreCollection : ICollectionFixture<EventStoreFixture> { }
}