using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Client
{

    /// <summary>
    /// Used to compose and send JSON RPC requests.
    /// </summary>
    public class JsonRpcClient
    {
        private readonly string requestIdPrefix;
        private int requestIdCounter = 0;

        /// <summary>
        /// Raises when a JSON RPC Request call is to be cancelled.
        /// </summary>
        /// <remarks>This event will not raise when a RPC notification has been cancelled.</remarks>
        public event EventHandler<RequestCancellingEventArgs> RequestCancelling;

        /// <summary>
        /// Initializes a JSON RPC client.
        /// </summary>
        /// <param name="handler">Handler used to transmit the messages.</param>
        public JsonRpcClient(IJsonRpcClientHandler handler)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            requestIdPrefix = RuntimeHelpers.GetHashCode(this) + "#";
        }

        /// <summary>
        /// Gets the handler used to transmit the messages.
        /// </summary>
        public IJsonRpcClientHandler Handler { get; }
        
        /// <summary>
        /// Generates the next unique value that can be used as <see cref="RequestMessage.Id"/>.
        /// </summary>
        public virtual MessageId NextRequestId()
        {
            var ct = Interlocked.Increment(ref requestIdCounter);
            return new MessageId(requestIdPrefix + ct);
        }

        /// <summary>
        /// Asynchronously send a JSON RPC notification message.
        /// </summary>
        /// <param name="methodName">RPC method name.</param>
        /// <param name="parameters">The parameters of the invocation. Can be null.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task contains the response of the request, or that contains <c>null</c> if the specified request does not need a response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="methodName"/> is <c>null</c>.</exception>
        public virtual Task<ResponseMessage> SendNotificationAsync(string methodName, JToken parameters, CancellationToken cancellationToken)
        {
            if (methodName == null) throw new ArgumentNullException(nameof(methodName));
            return SendAsync(new RequestMessage(methodName, parameters), cancellationToken);
        }

        /// <summary>
        /// Asynchronously send a JSON RPC request message.
        /// </summary>
        /// <param name="methodName">RPC method name.</param>
        /// <param name="parameters">The parameters of the invocation. Can be null.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task contains the response of the request, or that contains <c>null</c> if the specified request does not need a response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="methodName"/> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">(Can be <see cref="TaskCanceledException"/>.) The operation has been cancelled.</exception>
        public virtual Task<ResponseMessage> SendRequestAsync(string methodName, JToken parameters, CancellationToken cancellationToken)
        {
            if (methodName == null) throw new ArgumentNullException(nameof(methodName));
            return SendAsync(new RequestMessage(NextRequestId(), methodName, parameters), cancellationToken);
        }

        /// <summary>
        /// Asynchronously send a JSON RPC request or notification message.
        /// </summary>
        /// <param name="request">The request message to be sent.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task contains the response of the request, or that contains <c>null</c> if the specified request does not need a response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">A <paramref name="request"/> with the same id has been sent. You need to try with a different id.</exception>
        /// <exception cref="OperationCanceledException">(Can be <see cref="TaskCanceledException"/>.) The operation has been cancelled.</exception>
        public virtual async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            using (!request.IsNotification && cancellationToken.CanBeCanceled
                ? cancellationToken.Register(o => OnRequestCancelling((MessageId) o), request.Id)
                : default(CancellationTokenRegistration))
            {
                return await Handler.SendAsync(request, cancellationToken);
            }
        }

        /// <summary>
        /// Raises <see cref="RequestCancelling"/> event.
        /// </summary>
        protected virtual void OnRequestCancelling(MessageId id)
        {
            if (RequestCancelling != null)
            {
                var e = new RequestCancellingEventArgs(id);
                RequestCancelling?.Invoke(this, e);
            }
        }
    }

    /// <summary>
    /// Provides arguments for <see cref="JsonRpcClient.RequestCancelling"/> event.
    /// </summary>
    public class RequestCancellingEventArgs : EventArgs
    {
        public RequestCancellingEventArgs(MessageId requestId)
        {
            RequestId = requestId;
        }

        /// <summary>
        /// Id of the JSON RPC Request.
        /// </summary>
        public MessageId RequestId { get; }
    }
}
