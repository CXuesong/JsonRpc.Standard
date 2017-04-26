using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JsonRpc.Standard.Dataflow
{
    /// <summary>
    /// A buffered source dataflow block that continuously produces <see cref="Message"/>s.
    /// </summary>
    public abstract class BufferedMessageSourceBlock : IReceivableSourceBlock<Message>
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        protected BufferedMessageSourceBlock() : this(2)
        {
            
        }

        protected BufferedMessageSourceBlock(int bufferCapacity)
        {
            BufferBlock = new BufferBlock<Message>(new DataflowBlockOptions
            {
                BoundedCapacity = bufferCapacity,
            });
            var t = ReadMessagesAsync(cts.Token).ContinueWith(_ => cts.Dispose());
        }

        protected BufferBlock<Message> BufferBlock { get; }

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
            // We need to allow caller (.ctor) to finish the initialization
            // before actually start working.
            await Task.Yield();
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await ReadMessageAsync(cancellationToken).ConfigureAwait(false);
                    if (message == null) break; // EOF has been reached.
                    await BufferBlock.SendAsync(message, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                
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

        #region IReceivableSourceBlock

        /// <inheritdoc />
        public void Complete()
        {
            cts.Cancel();
        }

        /// <inheritdoc />
        void IDataflowBlock.Fault(Exception exception)
        {
            ((IDataflowBlock) BufferBlock).Fault(exception);
        }

        /// <inheritdoc />
        public Task Completion => BufferBlock.Completion;

        /// <inheritdoc />
        public IDisposable LinkTo(ITargetBlock<Message> target, DataflowLinkOptions linkOptions)
        {
            return BufferBlock.LinkTo(target, linkOptions);
        }

        /// <inheritdoc />
        Message ISourceBlock<Message>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<Message> target, out bool messageConsumed)
        {
            return ((IReceivableSourceBlock<Message>) BufferBlock).ConsumeMessage(messageHeader, target,
                out messageConsumed);
        }

        /// <inheritdoc />
        bool ISourceBlock<Message>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<Message> target)
        {
            return ((IReceivableSourceBlock<Message>)BufferBlock).ReserveMessage(messageHeader, target);
        }

        /// <inheritdoc />
        void ISourceBlock<Message>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<Message> target)
        {
            ((IReceivableSourceBlock<Message>)BufferBlock).ReleaseReservation(messageHeader, target);
        }

        /// <inheritdoc />
        public bool TryReceive(Predicate<Message> filter, out Message item)
        {
            return BufferBlock.TryReceive(filter, out item);
        }

        /// <inheritdoc />
        public bool TryReceiveAll(out IList<Message> items)
        {
            return BufferBlock.TryReceiveAll(out items);
        }

        #endregion
    }

}
