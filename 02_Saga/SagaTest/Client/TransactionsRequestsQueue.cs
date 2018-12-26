using System;
using System.Collections.Generic;
using System.Text;

namespace Client
{
    class TransactionsRequestsQueue
    {
        private Queue<Guid> Queue = new Queue<Guid>();

        public void Enqueue(Guid id)
        {
            Queue.Enqueue(id);
        }
        public Guid Dequeue()
        {
            return Queue.Dequeue();
        }

    }
}
