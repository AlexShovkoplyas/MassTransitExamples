using MassTransit;
using System;
using System.Threading.Tasks;

namespace Subscriber
{
    class Program
    {
        private const string HOST_NAME = "rabbitmq://localhost/";
        private const string FIRST_QUEUE_NAME = "first";
        private const string SECOND_QUEUE_NAME = "second";

        static async Task Main(string[] args)
        {
            var busControl = CreateBus();
            await busControl.StartAsync();

            Console.WriteLine("Consumer is started ...");
            Console.ReadLine();
            busControl.Stop();            
        }

        static IBusControl CreateBus()
        {
            return Bus.Factory.CreateUsingRabbitMq(x =>
            {
                x.Host(new Uri(HOST_NAME), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
                x.ReceiveEndpoint(FIRST_QUEUE_NAME, cfg =>
                {
                    cfg.Consumer<FirstConsumer>();
                    cfg.Consumer<SecondConsumer>();
                });
                x.ReceiveEndpoint(SECOND_QUEUE_NAME, cfg =>
                {                    
                    cfg.Consumer<ThirdConsumer>();
                });
            });
        }
    }
}
