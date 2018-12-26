using Contracts;
using MassTransit;
using System;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        private const string HOST_NAME = "rabbitmq://localhost/";
        private const string COMMAND_QUEUE_NAME = "transfer";
        private const string CONFIRMATION_QUEUE_NAME = "confirm";

        static async Task Main(string[] args)
        {
            var transactionsQueue = new TransactionsRequestsQueue();
            var bus = await CreateBusAsync(transactionsQueue);            

            for (int i = 0; i < 2; i++)
            {
                Console.WriteLine($"Transaction #{i}");

                await bus.Send<TransferMoney>(new
                {
                    AccountIdFrom = Guid.NewGuid(),
                    AccountIdTo = Guid.NewGuid(),
                    Amount = i * 100
                });
                Console.WriteLine("Transfer command was sent.");

                var code = Console.ReadLine();

                if (code.ToUpper() == "Q")
                {
                    await bus.Send<CancelTransaction>(new
                    {
                        TransactionId = transactionsQueue.Dequeue()
                    });
                }
                else
                {
                    await bus.Send<ConfirmTransaction>(new
                    {
                        TransactionId = transactionsQueue.Dequeue(),
                        Code = code
                    });
                }
                
                Console.WriteLine("Confirmation was sent");
            }           

            Console.WriteLine("Finish!");
            Console.ReadLine();
        }

        static async Task<IBusControl> CreateBusAsync(TransactionsRequestsQueue transactionsQueue)
        {
            var bus = Bus.Factory.CreateUsingRabbitMq(x => 
            {
                var host = x.Host(new Uri(HOST_NAME), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                x.ReceiveEndpoint(CONFIRMATION_QUEUE_NAME, cfg =>
                {
                    cfg.Consumer<ConfirmConsumer>(() => new ConfirmConsumer(transactionsQueue));
                });
            });

            await bus.StartAsync();

            EndpointConvention.Map<TransferMoney>(new Uri(HOST_NAME + COMMAND_QUEUE_NAME));
            EndpointConvention.Map<ConfirmTransaction>(new Uri(HOST_NAME + CONFIRMATION_QUEUE_NAME));

            return bus;
        }
    }
}
