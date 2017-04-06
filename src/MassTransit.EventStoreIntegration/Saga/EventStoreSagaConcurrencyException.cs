using System;

namespace MassTransit.EventStoreIntegration.Saga
{
    public class EventStoreSagaConcurrencyException : MassTransitException
    {
        public EventStoreSagaConcurrencyException()
        {
        }

        public EventStoreSagaConcurrencyException(string message)
            : base(message)
        {
        }

        public EventStoreSagaConcurrencyException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}