using System;
using MassTransit.Util;

namespace MassTransit.EventStoreIntegration.Sample
{
    class Program
    {
        static void Main()
        {
            var test = new Sample();

            TaskUtil.Await(() => test.Execute());

            Console.ReadLine();

            test.Stop();
        }
    }
}
