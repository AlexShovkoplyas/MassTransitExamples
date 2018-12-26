using System;
using System.Collections.Generic;
using System.Text;

namespace Contracts
{
    public class ConfirmTransaction
    {
        public Guid TransactionId { get; set; }
        public string Code { get; set; }
    }
}
