using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Represents a JSON RPC message reader.
    /// </summary>
    public abstract class MessageReader : IDisposable
    {
        /// <summary>
        /// Asynchronously reads the next message that matches the 
        /// </summary>
        /// <param name="filter">The expected type of the message.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>
        /// The next JSON RPC message, or <c>null</c> if no more messages exist.
        /// </returns>
        /// <remarks>This method should be thread-safe.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public abstract Task<Message> ReadAsync(Predicate<Message> filter, CancellationToken cancellationToken);

        private readonly CancellationTokenSource disposalTokenSource = new CancellationTokenSource();

        /// <summary>
        /// A <see cref="CancellationToken"/> that is cancelled when the current object has been disposed.
        /// </summary>
        protected CancellationToken DisposalToken => disposalTokenSource.Token;

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            // release unmanaged resources here
            if (disposing)
            {
                try
                {
                    disposalTokenSource.Cancel();
                }
                catch (AggregateException)
                {

                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposalTokenSource.IsCancellationRequested) return;
            Dispose(true);
            // GC.SuppressFinalize(this);
        }

        ///// <inheritdoc />
        //~MessageReader()
        //{
        //    Dispose(false);
        //}
    }

    /// <summary>
    /// A JSON RPC message reader that implements selective read by buffering all the
    /// received messages into a queue (or list) first.
    /// </summary>
    public abstract class QueuedMessageReader : MessageReader
    {
        private readonly LinkedList<Message> messages = new LinkedList<Message>();
        // used to lock `messages`
        private readonly SemaphoreSlim messagesLock = new SemaphoreSlim(1, 1);
        // used to ensure only 1 ReadDirectAsync is running at a time.
        private readonly SemaphoreSlim fetchMessagesLock = new SemaphoreSlim(1, 1);

        /// <inheritdoc />
        public override async Task<Message> ReadAsync(Predicate<Message> filter, CancellationToken cancellationToken)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            cancellationToken.ThrowIfCancellationRequested();
            if (DisposalToken.IsCancellationRequested)
                throw new ObjectDisposedException(nameof(PartwiseStreamMessageReader));
            LinkedListNode<Message> lastNode = null;
            using (var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposalToken))
            {
                try
                {
                    while (true)
                    {
                        await messagesLock.WaitAsync(linkedTokenSource.Token);
                        try
                        {
                            // Check if there's a satisfying item in the queue.
                            var node = lastNode?.Next == null ? messages.First : lastNode;
                            while (node != null)
                            {
                                if (filter(node.Value))
                                {
                                    messages.Remove(node);
                                    return node.Value;
                                }
                                node = node.Next;
                            }
                            lastNode = messages.Last;
                        }
                        finally
                        {
                            messagesLock.Release();
                        }
                        if (fetchMessagesLock.Wait(0))
                        {
                            // Fetch the next message, and decide whether it can pass the filter.
                            try
                            {
                                var next = await ReadDirectAsync(linkedTokenSource.Token);
                                if (next == null) return null; // EOF reached.
                                if (linkedTokenSource.IsCancellationRequested)
                                {
                                    // We don't want to lose any message because of cancellation…
                                    await messagesLock.WaitAsync(DisposalToken);
                                    try
                                    {
                                        messages.AddLast(next);
                                    }
                                    finally
                                    {
                                        messagesLock.Release();
                                    }
                                    linkedTokenSource.Token.ThrowIfCancellationRequested();
                                }
                                if (filter(next)) return next; // We're lucky.
                                // We still need to put the next item into the queue
                                // ReSharper disable once MethodSupportsCancellation
                                await messagesLock.WaitAsync(DisposalToken);
                                try
                                {
                                    messages.AddLast(next);
                                }
                                finally
                                {
                                    if (!DisposalToken.IsCancellationRequested)
                                        messagesLock.Release();
                                }
                                // After that, we can check the cancellation
                                linkedTokenSource.Token.ThrowIfCancellationRequested();
                            }
                            finally
                            {
                                if (!DisposalToken.IsCancellationRequested)
                                    fetchMessagesLock.Release();
                            }
                        }
                        else
                        {
                            // Or wait for the next message to come.
                            linkedTokenSource.Token.ThrowIfCancellationRequested();
                            await fetchMessagesLock.WaitAsync(linkedTokenSource.Token);
                            fetchMessagesLock.Release();
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    linkedTokenSource.Token.ThrowIfCancellationRequested();
                    throw;
                }
            }
        }

        /// <summary>
        /// When overridden in the derived class, directly asynchronously reads the next message.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation OR indicates the current instance has just been disposed.</param>
        /// <returns>The message just read, or <c>null</c> if EOF has reached.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <remarks>The caller has guaranteed there is at most 1 ongoing call of this method.</remarks>
        protected abstract Task<Message> ReadDirectAsync(CancellationToken cancellationToken);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                fetchMessagesLock.Dispose();
                messagesLock.Dispose();
            }
        }
    }
}
