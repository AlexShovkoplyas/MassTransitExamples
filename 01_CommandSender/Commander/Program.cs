using Contracts;
using MassTransit;
using MassTransit.Util;
using System;
using System.Threading.Tasks;

namespace Commander
{
    class Program
    {
        private const string HOST_NAME = "rabbitmq://localhost/";
        private const string FIRST_QUEUE_NAME = "first";
        private const string SECOND_QUEUE_NAME = "second";

        static async Task Main(string[] args)
        {
            IBusControl busControl = CreateBus();

            //same result, sends to exchange with queue's name
            //requires prior subscription

            //await SendWithConventions(busControl);

            await SendToUriEndpoints(busControl);

            Console.ReadLine();
            busControl.Stop();
        }

        static IBusControl CreateBus()
        {
            return Bus.Factory.CreateUsingRabbitMq(x => x.Host(new Uri(HOST_NAME), h =>
            {
                h.Username("guest");
                h.Password("guest");
            }));
        }

        static async Task SendWithConventions(IBusControl busControl)
        {
            EndpointConvention.Map<FirstCommand>(new Uri(HOST_NAME + FIRST_QUEUE_NAME));
            EndpointConvention.Map<SecondCommand>(new Uri(HOST_NAME + SECOND_QUEUE_NAME));

            await busControl.StartAsync();

            await busControl.Send<FirstCommand>(new { Version = 2 });
            Console.WriteLine("First command was sent");

            Console.ReadLine();

            await busControl.Send<SecondCommand>(new { Name = "Alex" });
            Console.WriteLine("Second command was sent");            
        }

        private static async Task SendToUriEndpoints(IBusControl busControl)
        {
            await busControl.StartAsync();

            var firstEndpoint = await busControl.GetSendEndpoint(new Uri(HOST_NAME + FIRST_QUEUE_NAME)); 
            await firstEndpoint.Send<FirstCommand>(new { Version = 2 });
            Console.WriteLine("First command was sent");

            Console.ReadLine();

            var secondEndpoint = await busControl.GetSendEndpoint(new Uri(HOST_NAME + SECOND_QUEUE_NAME));
            await secondEndpoint.Send<SecondCommand>(new { Name = "Alex" });
            Console.WriteLine("Second command was sent");
        }
    }
}
