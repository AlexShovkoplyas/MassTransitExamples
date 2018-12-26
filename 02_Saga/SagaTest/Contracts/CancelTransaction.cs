using System;
using System.Collections.Generic;
using System.Text;

namespace Contracts
{
    public class CancelTransaction
    {
        public Guid TransactionId { get; set; }
    }
}
