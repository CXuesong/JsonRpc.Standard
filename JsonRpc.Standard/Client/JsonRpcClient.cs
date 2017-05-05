using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard.Client
{

    /// <summary>
    /// Provides options for <see cref="JsonRpcClient"/>.
    /// </summary>
    [Flags]
    public enum JsonRpcClientOptions
    {
        /// <summary>
        /// No special configurations.
        /// </summary>
        None = 0,

        /// <summary>
        /// Preserves the responses whose Id dosn't match any requests sent by this client.
        /// The default behavior will just discard them.
        /// </summary>
        PreserveForeignResponses
    }

    /// <summary>
    /// Used to compose and send JSON RPC requests.
    /// </summary>
    public class JsonRpcClient
    {
        private readonly string requestIdPrefix;
        private int requestIdCounter = 0;

        private readonly Dictionary<MessageId, TaskCompletionSource<ResponseMessage>> impendingRequestDict
            = new Dictionary<MessageId, TaskCompletionSource<ResponseMessage>>();

        /// <summary>
        /// Raises when a JSON RPC message will be sent.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageSending;

        /// <summary>
        /// Raises when a JSON RPC message will be received.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceiving;

        /// <summary>
        /// Raises when a JSON RPC call is to be cancelled.
        /// </summary>
        public event EventHandler<RequestCancellingEventArgs> RequestCancelling;


        /// <summary>
        /// Initializes a JSON RPC client.
        /// </summary>
        public JsonRpcClient() : this(JsonRpcClientOptions.None)
        {
        }

        /// <summary>
        /// Initializes a JSON RPC client.
        /// </summary>
        /// <param name="options">Client options.</param>
        public JsonRpcClient(JsonRpcClientOptions options)
        {
            Options = options;
            requestIdPrefix = RuntimeHelpers.GetHashCode(this) + "#";
            OutBufferBlock = new BufferBlock<RequestMessage>();
            InBufferBlock = new ActionBlock<Message>(message =>
            {
                var resp = message as ResponseMessage;
                Debug.Assert(resp != null);
                if (resp == null) return;
                OnMessageReceiving(resp);
                TaskCompletionSource<ResponseMessage> tcs;
                lock (impendingRequestDict)
                {
                    // We might be going to discard the foreign response.
                    if (!impendingRequestDict.TryGetValue(resp.Id, out tcs)) return;
                    impendingRequestDict.Remove(resp.Id);
                }
                tcs.TrySetResult(resp);
            });
        }

        /// <summary>
        /// Client options.
        /// </summary>
        public JsonRpcClientOptions Options { get; }

        /// <summary>
        /// The input buffer used to receive responses.
        /// </summary>
        protected ITargetBlock<Message> InBufferBlock { get; }

        /// <summary>
        /// The output buffer used to emit requests.
        /// </summary>
        protected BufferBlock<RequestMessage> OutBufferBlock { get; }

        /// <summary>
        /// Attaches the client to the specific source block and target block.
        /// </summary>
        /// <param name="source">The source block used to retrieve the responses.</param>
        /// <param name="target">The target block used to emit the requests.</param>
        /// <returns>A <see cref="IDisposable"/> used to disconnect the source and target blocks.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="source"/> or <paramref name="target"/> is <c>null</c>.</exception>
        public IDisposable Attach(ISourceBlock<Message> source, ITargetBlock<Message> target)
        {
            // so client is not a propagation block…
            // OutBufferBlock --> writer
            //                       |
            //  <------ SERVER ------<
            //  |
            // reader --> InBufferBlock
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            var d1 = source.LinkTo(InBufferBlock, m =>
            {
                var resp = m as ResponseMessage;
                if (resp == null) return false;
                // Discard foreign responses, if any.
                if ((Options & JsonRpcClientOptions.PreserveForeignResponses) !=
                    JsonRpcClientOptions.PreserveForeignResponses) return true;
                // Or we will check if we're wating for this response.
                lock (impendingRequestDict) return impendingRequestDict.ContainsKey(resp.Id);
            });
            var d2 = OutBufferBlock.LinkTo(target);
            return Utility.CombineDisposable(d1, d2);
        }

        /// <summary>
        /// Generates the next unique value that can be used as <see cref="RequestMessage.Id"/>.
        /// </summary>
        public MessageId NextRequestId()
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
        public Task<ResponseMessage> SendNotificationAsync(string methodName, JToken parameters, CancellationToken cancellationToken)
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
        public Task<ResponseMessage> SendRequestAsync(string methodName, JToken parameters, CancellationToken cancellationToken)
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
        public async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<ResponseMessage> tcs = null;
            CancellationTokenRegistration ctr;
            if (!request.IsNotification)
            {
                // We need to monitor the response.
                tcs = new TaskCompletionSource<ResponseMessage>();
                lock (impendingRequestDict) impendingRequestDict.Add(request.Id, tcs);
                ctr = cancellationToken.Register(o =>
                {
                    TaskCompletionSource<ResponseMessage> tcs1;
                    var id = (MessageId) o;
                    OnRequestCancelling(id);
                    lock (impendingRequestDict)
                        if (!impendingRequestDict.TryGetValue(id, out tcs1)) return;
                    if (!tcs1.TrySetCanceled()) return;
                    // Note that server might still send the response after cancellation on the client.
                    // If we are going to keep all "foreign" responses, we need to be able to recgnize it later.
                    var keepRequestIdInMind = (Options & JsonRpcClientOptions.PreserveForeignResponses) ==
                                              JsonRpcClientOptions.PreserveForeignResponses;
                    if (keepRequestIdInMind)
                    {
#pragma warning disable 4014
                        // ReSharper disable MethodSupportsCancellation
                        Task.Delay(60000)
                            .ContinueWith((_, o1) =>
                            {
                                lock (impendingRequestDict) impendingRequestDict.Remove((MessageId) o1);
                            }, o);
                        // ReSharper restore MethodSupportsCancellation
#pragma warning restore 4014
                    }
                    else
                    {
                        lock (impendingRequestDict) impendingRequestDict.Remove(id);
                    }
                }, request.Id);
            }
            try
            {
                OnMessageSending(request);
                await OutBufferBlock.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Exception when sending the request…
                if (!request.IsNotification)
                {
                    ctr.Dispose();
                    lock (impendingRequestDict) impendingRequestDict.Remove(request.Id);
                }
                throw;
            }
            if (request.IsNotification) return null;
            // Now wait for the response.
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                ctr.Dispose();
            }
        }

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

        public MessageId RequestId { get; }
    }
}
