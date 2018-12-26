using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;

namespace Client
{
    internal class ConfirmConsumer : IConsumer<TransactionConfirmationRequested>
    {
        private readonly TransactionsRequestsQueue queue;

        public ConfirmConsumer(TransactionsRequestsQueue queue)
        {
            this.queue = queue;
        }

        public Task Consume(ConsumeContext<TransactionConfirmationRequested> context)
        {
            Console.WriteLine("Confirmation code was requested");
            queue.Enqueue(context.Message.TransactionId);
            Console.WriteLine("Please enter code (1111) :");
            return Task.CompletedTask;
        }
    }
}