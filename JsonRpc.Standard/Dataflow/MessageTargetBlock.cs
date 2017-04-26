using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JsonRpc.Standard.Dataflow
{
    /// <summary>
    /// A buffered target dataflow block that continuously retrieve <see cref="Message"/>s.
    /// </summary>
    public abstract class BufferedMessageTargetBlock : ITargetBlock<Message>
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        protected BufferedMessageTargetBlock() : this(2)
        {

        }

        protected BufferedMessageTargetBlock(int bufferCapacity)
        {
            BufferBlock = new BufferBlock<Message>(new DataflowBlockOptions
            {
                BoundedCapacity = bufferCapacity,
                CancellationToken = cts.Token
            });
            var t = WriteMessagesAsync(cts.Token).ContinueWith(_ => cts.Dispose());
        }

        protected BufferBlock<Message> BufferBlock { get; }

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
            // We need to allow caller (.ctor) to finish the initialization
            // before actually start working.
            await Task.Yield();
            try
            {
                if (cancellationToken.IsCancellationRequested) return;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await BufferBlock.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    if (message != null)
                        await WriteMessageAsync(message, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                ((ITargetBlock<Message>) BufferBlock).Fault(ex);
            }
            finally
            {
                BufferBlock.Complete();
            }
        }

        #region ITargetBlock

        /// <inheritdoc />
        DataflowMessageStatus ITargetBlock<Message>.OfferMessage(DataflowMessageHeader messageHeader, Message messageValue,
            ISourceBlock<Message> source,
            bool consumeToAccept)
        {
            return ((ITargetBlock<Message>) BufferBlock).OfferMessage(messageHeader, messageValue, source,
                consumeToAccept);
        }

        /// <inheritdoc />
        public void Complete()
        {
            cts.Cancel();
            BufferBlock.Complete();
        }

        /// <inheritdoc />
        void IDataflowBlock.Fault(Exception exception)
        {
            ((IDataflowBlock) BufferBlock).Fault(exception);
        }

        /// <inheritdoc />
        public Task Completion
        {
            get { return BufferBlock.Completion; }
        }

        #endregion
    }
}
