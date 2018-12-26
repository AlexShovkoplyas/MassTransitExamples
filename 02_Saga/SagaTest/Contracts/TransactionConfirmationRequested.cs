using System;
using System.Collections.Generic;
using System.Text;

namespace Contracts
{
    public class TransactionConfirmationRequested
    {
        public Guid TransactionId { get; set; }
    }
}
