using Contracts;
using MassTransit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Subscriber
{
    class FirstConsumer : IConsumer<FirstCommand>
    {
        public Task Consume(ConsumeContext<FirstCommand> context)
        {
            Console.WriteLine($"FirstConsumer --- FirstCommand received... version : {context.Message.Version}");
            return Task.CompletedTask;
        }
    }

    class SecondConsumer : IConsumer<SecondCommand>
    {
        public Task Consume(ConsumeContext<SecondCommand> context)
        {
            Console.WriteLine($"SecondConsumer --- SecondCommand received... Name : {context.Message.Name}");
            return Task.CompletedTask;
        }
    }

    class ThirdConsumer : IConsumer<SecondCommand>
    {
        public Task Consume(ConsumeContext<SecondCommand> context)
        {
            Console.WriteLine($"ThirdConsumer --- SecondCommand received... Name : {context.Message.Name}");
            return Task.CompletedTask;
        }
    }
}
