using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;

namespace UnitTestProject1
{
    class QueueMessageReader : QueuedMessageReader
    {

        public QueueMessageReader(ConcurrentQueue<Message> queue)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            Queue = queue;
        }

        public ConcurrentQueue<Message> Queue { get; }

        /// <inheritdoc />
        protected override async Task<Message> ReadDirectAsync(CancellationToken cancellationToken)
        {
            Message msg;
            while (!Queue.TryDequeue(out msg))
                await Task.Delay(10, cancellationToken);
            return msg;
        }
    }

    class QueueMessageWriter : MessageWriter
    {
        public QueueMessageWriter(ConcurrentQueue<Message> queue)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            Queue = queue;
        }

        public ConcurrentQueue<Message> Queue { get; }

        /// <inheritdoc />
        public override Task WriteAsync(Message message, CancellationToken cancellationToken)
        {
            Queue.Enqueue(message);
            return Task.FromResult(true);
        }
    }
}
