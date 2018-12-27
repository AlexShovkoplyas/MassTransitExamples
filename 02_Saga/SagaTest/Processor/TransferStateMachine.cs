using Automatonymous;
using Automatonymous.Binders;
using Contracts;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Processor
{
    public class TransferStateMachine : MassTransitStateMachine<TransferState>
    {
        public TransferStateMachine()
        {
            InstanceState(x => x.CurrentState);

            Event(() => Opened, x => x.SelectId(context => Guid.NewGuid()));
            Event(() => ConfirmationRequested, x => x.CorrelateById(context => context.Message.TransactionId));
            Event(() => Canceled, x => x.CorrelateById(context => context.Message.TransactionId));

            Schedule(() => TransactionExpired, x => x.ExpirationId, x =>
            {
                x.Delay = TimeSpan.FromSeconds(10);
                x.Received = e => e.CorrelateById(context => context.Message.TransactionId);
            });

            Initially(
                When(Opened)
                    .Then(InitializeState)
                    .Then(SetConfirmationCode)
                    .Publish(ConfirmationRequestedFactory)
                    .Schedule(TransactionExpired, TransactionExpiredFactory)
                    .TransitionTo(Pending)
                );

            During(Pending,
                When(ConfirmationRequested)
                    .Then(ValidateConfirmationCode)
                    .Then(context => SaveLog(context.Instance))
                    .Finalize(),
                When(Canceled)
                    .Then(CancelThisTransaction)
                    .Then(context => SaveLog(context.Instance))
                    .Finalize(),
                When(TransactionExpired.Received)
                    .Then(ExpireTransaction)
                    .Then(context => SaveLog(context.Instance))
                    .Finalize()
                );

            SetCompletedWhenFinalized();
        }

        public State Pending { get; private set; }
        //public State Approved { get; private set; }
        //public State Finished { get; private set; }

        public Schedule<TransferState, TransactionExpired> TransactionExpired { get; private set; }

        public Event<TransferMoney> Opened { get; private set; }
        public Event<CancelTransaction> Canceled { get; private set; }
        public Event<ConfirmTransaction> ConfirmationRequested { get; private set; }

        private void InitializeState(BehaviorContext<TransferState, TransferMoney> context)
        {
            context.Instance.AccountIdFrom = context.Data.AccountIdFrom;
            context.Instance.AccountIdTo = context.Data.AccountIdTo;
            context.Instance.Amount = context.Data.Amount;
            context.Instance.Created = DateTime.Now;
        }

        private void SetConfirmationCode(BehaviorContext<TransferState, TransferMoney> context)
        {
            Thread.Sleep(50);
            context.Instance.ConfirmationCode = "1111";
        }

        private TransactionConfirmationRequested ConfirmationRequestedFactory(ConsumeEventContext<TransferState, TransferMoney> context)
        {
            Console.WriteLine("Code confirmation was requested");
            return new TransactionConfirmationRequested
            {
                TransactionId = context.Instance.CorrelationId
            };
        }

        private TransactionExpired TransactionExpiredFactory(ConsumeEventContext<TransferState, TransferMoney> context)
        {
            Console.WriteLine("Transaction expiretion was scheduled");
            return new TransactionExpired
            {
                TransactionId = context.Instance.CorrelationId
            };
        }

        private void ValidateConfirmationCode(BehaviorContext<TransferState, ConfirmTransaction> context)
        {
            if (context.Instance.ConfirmationCode == context.Data.Code)
            {
                Console.WriteLine("Code is confirmed");
                context.Instance.isConfirmed = true;
            }
            else
            {
                Console.WriteLine("Code is wrong");
                context.Instance.isConfirmed = false;
            }
        }

        private void CancelThisTransaction(BehaviorContext<TransferState, CancelTransaction> context)
        {
            Console.WriteLine("Transaction is canceled");
            context.Instance.isCanceled = true;
        }

        private void ExpireTransaction(BehaviorContext<TransferState, TransactionExpired> context)
        {
            Console.WriteLine("Transaction was expired");
            context.Instance.isExpired = true;
        }

        private void SaveLog(TransferState state)
        {
            Thread.Sleep(50);
            Console.WriteLine("Saving log...");
        }
    }    
}
