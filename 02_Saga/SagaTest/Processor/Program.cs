using Automatonymous;
using Contracts;
using MassTransit;
using MassTransit.QuartzIntegration;
using MassTransit.Saga;
using MassTransit.Util;
using Quartz;
using Quartz.Impl;
using System;
using System.Threading.Tasks;

namespace Processor
{
    class Program
    {
        private const string HOST_NAME = "rabbitmq://localhost/";
        private const string COMMAND_QUEUE_NAME = "transfer";
        private const string CONFIRMATION_QUEUE_NAME = "confirm";

        static async Task Main(string[] args)
        {
            var machine = new TransferStateMachine();
            var repository = new InMemorySagaRepository<TransferState>();

            //var scheduler = await CreateSchedulerAsync();

            var bus = await CreateBusAsync(machine, repository);
            //var bus = await CreateBusAsync(machine, repository, scheduler);

            Console.WriteLine("Saga consumer is started...");
            Console.ReadLine();
        }

        static async Task<IBusControl> CreateBusAsync<T>(MassTransitStateMachine<T> machine, ISagaRepository<T> repository) 
            where T: class, SagaStateMachineInstance
        {
            var bus = Bus.Factory.CreateUsingRabbitMq(x => 
            {
                x.Host(new Uri(HOST_NAME), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
                x.ReceiveEndpoint(COMMAND_QUEUE_NAME, cfg =>
                {
                    cfg.StateMachineSaga(machine, repository);
                });
                x.ReceiveEndpoint(CONFIRMATION_QUEUE_NAME, cfg =>
                {
                    cfg.StateMachineSaga(machine, repository);
                });

                //x.ReceiveEndpoint("sample-quartz-scheduler", cfg =>
                //{
                //    x.UseMessageScheduler(cfg.InputAddress);

                //    cfg.Consumer(() => new ScheduleMessageConsumer(scheduler));
                //    cfg.Consumer(() => new CancelScheduledMessageConsumer(scheduler));
                //});
            });            

            await bus.StartAsync();

            //scheduler.JobFactory = new MassTransitJobFactory(bus);

            //await scheduler.Start();

            return bus;
        }

        static async Task<IScheduler> CreateSchedulerAsync()
        {
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            IScheduler scheduler = await schedulerFactory.GetScheduler();

            return scheduler;
        }
    }
}
