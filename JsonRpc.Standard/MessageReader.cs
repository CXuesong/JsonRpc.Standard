using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents a JSON RPC message reader.
    /// </summary>
    public abstract class MessageReader
    {
        /// <summary>
        /// Asynchronously reads the next message.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>
        /// The next JSON RPC message, or <c>null</c> if no more messages exist.
        /// </returns>
        /// <remarks>This method should be thread-safe.</remarks>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public abstract Task<Message> ReadAsync(CancellationToken cancellationToken);

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
    }

    /// <summary>
    /// A JSON RPC message reader that implements selective read by buffering all the
    /// received messages into a queue (or list) first.
    /// </summary>
    public abstract class QueuedMessageReader : MessageReader
    {
        private readonly LinkedList<Message> messages = new LinkedList<Message>();
        private readonly SemaphoreSlim messagesLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim fetchMessagesLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Asynchronously reads the next message.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>
        /// The next JSON RPC message, or <c>null</c> if no more messages exist.
        /// </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public override Task<Message> ReadAsync(CancellationToken cancellationToken)
        {
            return ReadAsync(_ => true, cancellationToken);
        }

        /// <summary>
        /// Asynchronously reads the next message that matches the 
        /// </summary>
        /// <param name="filter">The expected type of the message.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>
        /// The next JSON RPC message, or <c>null</c> if no more messages exist.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public override async Task<Message> ReadAsync(Predicate<Message> filter, CancellationToken cancellationToken)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            cancellationToken.ThrowIfCancellationRequested();
            LinkedListNode<Message> lastNode = null;
            while (true)
            {
                using (await messagesLock.LockAsync(cancellationToken))
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
                if (fetchMessagesLock.Wait(0))
                {
                    // Fetch the next message, and decide whether it can pass the filter.
                    try
                    {
                        var next = await ReadDirectAsync(cancellationToken);
                        if (filter(next)) return next; // We're lucky.
                        // We still need to put the next item into the queue
                        // ReSharper disable once MethodSupportsCancellation
                        using (await messagesLock.LockAsync())
                            messages.AddLast(next);
                        // After that, we can check the cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    finally
                    {
                        fetchMessagesLock.Release();
                    }
                }
                else
                {
                    // Or wait for the next message to come.
                    await messagesLock.WaitAsync(cancellationToken);
                    messagesLock.Release();
                }
            }
        }

        /// <summary>
        /// When overridden in the derived class, directly asynchronously reads the next message.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected abstract Task<Message> ReadDirectAsync(CancellationToken cancellationToken);
    }
}
