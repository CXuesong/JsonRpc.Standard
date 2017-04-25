using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents a JSON RPC message writer.
    /// </summary>
    public abstract class MessageWriter
    {
        /// <summary>
        /// The target block used to receive messages to be written.
        /// </summary>
        public abstract ITargetBlock<Message> TargetBlock { get; }
    }

    /// <summary>
    /// Represents a JSON RPC message writer.
    /// </summary>
    public abstract class BufferedMessageWriter : MessageWriter
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        protected BufferedMessageWriter() : this(16)
        {

        }

        protected BufferedMessageWriter(int bufferCapacity)
        {
            BufferBlock = new BufferBlock<Message>(new DataflowBlockOptions
            {
                BoundedCapacity = bufferCapacity,
                CancellationToken = cts.Token
            });
            var t = WriteMessagesAsync(cts.Token).ContinueWith(_ => cts.Dispose());
        }

        public override ITargetBlock<Message> TargetBlock => BufferBlock;

        protected BufferBlock<Message> BufferBlock { get; }

        public void Stop()
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Asynchrnously writes the next message.
        /// </summary>
        /// <param name="message">The message to be write.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that completes when the message has been written.</returns>
        protected abstract Task WriteMessageAsync(Message message, CancellationToken cancellationToken);

        /// <summary>
        /// The main loop that pumps the messages into <see cref="BufferBlock"/>.
        /// </summary>
        /// <param name="cancellationToken"></param>
        protected async Task WriteMessagesAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await BufferBlock.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (message != null)
                    await WriteMessageAsync(message, cancellationToken);
            }
        }
    }
}
