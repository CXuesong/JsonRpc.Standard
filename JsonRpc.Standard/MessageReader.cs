using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents a JSON RPC message reader.
    /// </summary>
    public abstract class MessageReader
    {
        /// <summary>
        /// The source block used to emit the messages.
        /// </summary>
        public abstract ISourceBlock<Message> SourceBlock { get; }
    }

    /// <summary>
    /// Represents a JSON RPC message reader.
    /// </summary>
    public abstract class BufferedMessageReader : MessageReader
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        protected BufferedMessageReader() : this(16)
        {
            
        }

        protected BufferedMessageReader(int bufferCapacity)
        {
            BufferBlock = new BufferBlock<Message>(new DataflowBlockOptions
            {
                BoundedCapacity = bufferCapacity,
                CancellationToken = cts.Token
            });
            var t = ReadMessagesAsync(cts.Token).ContinueWith(_ => cts.Dispose());
        }

        public override ISourceBlock<Message> SourceBlock => BufferBlock;

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
        /// Asynchrnously reads the next message.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that returns the next message, or <c>null</c> if EOF has been reached.</returns>
        protected abstract Task<Message> ReadMessageAsync(CancellationToken cancellationToken);

        /// <summary>
        /// The main loop that pumps the messages into <see cref="BufferBlock"/>.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the request.</param>
        protected async Task ReadMessagesAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(cancellationToken).ConfigureAwait(false);
                if (message == null) break;     // EOF has been reached.
                await BufferBlock.SendAsync(message, cancellationToken);
            }
            BufferBlock.Complete();
        }

    }

}
