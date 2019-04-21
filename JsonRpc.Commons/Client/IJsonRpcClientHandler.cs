using System;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;

namespace JsonRpc.Client
{
    /// <summary>
    /// Provides methods for transmitting the client-side JSON RPC messages.
    /// </summary>
    /// <remarks>Implementation Notes: Consider inheriting from <see cref="JsonRpcClientHandler"/> instead of directly implementing this interface.</remarks>
    public interface IJsonRpcClientHandler
    {

        /// <summary>
        /// Asynchronously sends a JSON RPC Request message, and wait for the Response (if the Request is not a Notification).
        /// </summary>
        /// <param name="request">The request message to be sent.</param>
        /// <param name="cancellationToken">A token used to cancel the transmitting request, or to stop waiting for the Response.</param>
        /// <returns>A task that returns JSON RPC response, or <c>null</c> if the Request is a Notification.</returns>
        /// <remarks>
        /// If a JSON RPC Request has already been sent, cancellation via
        /// <paramref name="cancellationToken"/> will only make the returned task stop waiting for the response.
        /// To actually notifies the RPC server to cancel certain request, both the client and server side should
        /// make a contract on how to cancel an ongoing request, for example, by sending a special "cancellation"
        /// notification.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="JsonRpcClientException">An exception has occurred while transmitting the request.
        /// Note that a JSON RPC Response with Error will be returned and no exception should be thrown.</exception>
        Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Provides basic infrastructures for <see cref="IJsonRpcClientHandler"/> implementation.
    /// </summary>
    public abstract class JsonRpcClientHandler : IJsonRpcClientHandler
    {
        /// <inheritdoc />
        public abstract Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken);

        /// <summary>
        /// Raises when a JSON RPC message will be sent.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageSending;

        /// <summary>
        /// Raises when a JSON RPC message will be received.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceiving;

        /// <summary>
        /// Raises <see cref="MessageSending"/> event.
        /// </summary>
        protected virtual void OnMessageSending(RequestMessage message)
        {
            if (MessageSending != null)
            {
                var e = new MessageEventArgs(message);
                MessageSending?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Raises <see cref="MessageReceiving"/> event.
        /// </summary>
        protected virtual void OnMessageReceiving(ResponseMessage message)
        {
            if (MessageReceiving != null)
            {
                var e = new MessageEventArgs(message);
                MessageReceiving?.Invoke(this, e);
            }
        }
    }
}
