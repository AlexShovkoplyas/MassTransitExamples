using System;
using System.Collections.Generic;
using System.Text;

namespace Contracts
{
    public class TransferMoney
    {
        public Guid AccountIdFrom { get; set; }
        public Guid AccountIdTo { get; set; }
        public int Amount { get; set; }
    }
}
