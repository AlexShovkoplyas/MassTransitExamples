using Automatonymous;
using System;
using System.Collections.Generic;
using System.Text;

namespace Processor
{
    class TransferState : SagaStateMachineInstance
    {
        protected TransferState()
        {
        }

        public TransferState(Guid correlationId)
        {
            CorrelationId = correlationId;
        }

        public string CurrentState { get; set; }

        public Guid CorrelationId { get; set; }

        public Guid AccountIdFrom { get; set; }

        public Guid AccountIdTo { get; set; }

        public int Amount { get; set; }

        public string ConfirmationCode { get; set; }

        public Guid? ExpirationId { get; set; }

        public DateTime Created { get; set; }
    }
}
