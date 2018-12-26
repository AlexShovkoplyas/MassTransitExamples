using Automatonymous;
using Contracts;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Processor
{
    class TransferStateMachine : MassTransitStateMachine<TransferState>
    {
        public TransferStateMachine()
        {
            InstanceState(x => x.CurrentState, Active, Finished);

            Event(() => Opened, x => x.SelectId(context => Guid.NewGuid()));
            Event(() => Confirmed, x => x.CorrelateById(context => context.Message.TransactionId));
            Event(() => Canceled, x => x.CorrelateById(context => context.Message.TransactionId));

            Schedule(() => TransactionExpired, x => x.ExpirationId, x =>
            {
                x.Delay = TimeSpan.FromSeconds(10);
                x.Received = e => e.CorrelateById(context => context.Message.TransactionId);
            });

            Initially(
                When(Opened)
                    .Then(context =>
                    {
                        context.Instance.AccountIdFrom = context.Data.AccountIdFrom;
                        context.Instance.AccountIdTo = context.Data.AccountIdTo;
                        context.Instance.Amount = context.Data.Amount;

                        context.Instance.Created = DateTime.Now;

                        context.Instance.ConfirmationCode = "1111";
                    })
                    .ThenAsync(async context =>
                    {
                        //send confirmation request
                        await context.Publish(new TransactionConfirmationRequested()
                        {
                            TransactionId = context.Instance.CorrelationId
                        });

                        await Console.Out.WriteLineAsync($"Money Transfer requested {context.Data.AccountIdFrom} to {context.Data.AccountIdTo}");
                        await Console.Out.WriteLineAsync($"Money Transfer requested correlationId: {context.Instance.CorrelationId}");
                    })
                    .Schedule(TransactionExpired, context => new TransactionExpiredEvent(context.Instance))
                    .TransitionTo(Active)
                );

            During(Active,
                When(Confirmed)
                    .ThenAsync(async context =>
                    {
                        //todo: check if code is correct
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    })
                    .ThenAsync(context => Console.Out.WriteLineAsync($"Transaction Finished: {context.Instance.CorrelationId}"))
                    .TransitionTo(Finished),
                When(Canceled)
                    .ThenAsync(context => Console.Out.WriteLineAsync($"Transaction Finished: {context.Instance.CorrelationId}"))
                    .TransitionTo(Finished),
                When(TransactionExpired.Received)
                    .ThenAsync(context => Console.Out.WriteLineAsync($"Transaction Expired: {context.Instance.CorrelationId}"))
                    .Publish(context => new TransactionExpiredEvent(context.Instance))
                    .Finalize()
                );

            SetCompletedWhenFinalized();
        }

        public State Active { get; private set; }
        public State Finished { get; private set; }

        public Schedule<TransferState, TransactionExpired> TransactionExpired { get; private set; }

        public Event<TransferMoney> Opened { get; private set; }
        public Event<CancelTransaction> Canceled { get; private set; }
        public Event<ConfirmTransaction> Confirmed { get; private set; }

        class TransactionExpiredEvent : TransactionExpired
        {
            readonly TransferState state;

            public TransactionExpiredEvent(TransferState state)
            {
                this.state = state;
            }

            public Guid TransactionId => state.CorrelationId;
        }

        class TransactionCanceledEvent : TransactionCanceled
        {
            readonly TransferState state;

            public TransactionCanceledEvent(TransferState state)
            {
                this.state = state;
            }

            public Guid TransactionId => state.CorrelationId;
        }
    }    
}
