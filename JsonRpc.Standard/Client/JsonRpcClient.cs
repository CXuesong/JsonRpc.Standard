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

        private readonly Dictionary<object, TaskCompletionSource<ResponseMessage>> impendingRequestDict
            = new Dictionary<object, TaskCompletionSource<ResponseMessage>>();

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
            OutBufferBlock = new BufferBlock<GeneralRequestMessage>();
            InBufferBlock = new ActionBlock<Message>(message =>
            {
                var resp = message as ResponseMessage;
                Debug.Assert(resp != null);
                if (resp == null) return;
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
        protected BufferBlock<GeneralRequestMessage> OutBufferBlock { get; }

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
        public object NextRequestId()
        {
            var ct = Interlocked.Increment(ref requestIdCounter);
            return requestIdPrefix + ct;
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
            return SendAsync(new NotificationMessage(methodName, parameters), cancellationToken);
        }

        /// <summary>
        /// Asynchronously send a JSON RPC request message.
        /// </summary>
        /// <param name="methodName">RPC method name.</param>
        /// <param name="parameters">The parameters of the invocation. Can be null.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task contains the response of the request, or that contains <c>null</c> if the specified request does not need a response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="methodName"/> is <c>null</c>.</exception>
        public Task<ResponseMessage> SendRequestAsync(string methodName, JToken parameters, CancellationToken cancellationToken)
        {
            if (methodName == null) throw new ArgumentNullException(nameof(methodName));
            return SendAsync(new RequestMessage(NextRequestId(), methodName, parameters), cancellationToken);
        }

        /// <summary>
        /// Asynchronously send a JSON RPC request or notification message.
        /// </summary>
        /// <param name="message">The request message to be sent.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task contains the response of the request, or that contains <c>null</c> if the specified request does not need a response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
        public async Task<ResponseMessage> SendAsync(GeneralRequestMessage message, CancellationToken cancellationToken)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            cancellationToken.ThrowIfCancellationRequested();
            var request = message as RequestMessage;
            TaskCompletionSource<ResponseMessage> tcs = null;
            CancellationTokenRegistration ctr;
            if (request != null)
            {
                tcs = new TaskCompletionSource<ResponseMessage>();
                lock (impendingRequestDict) impendingRequestDict.Add(request.Id, tcs);
                ctr = cancellationToken.Register(o =>
                {
                    TaskCompletionSource<ResponseMessage> tcs1;
                    lock (impendingRequestDict)
                        if (!impendingRequestDict.TryGetValue(o, out tcs1)) return;
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
                                lock (impendingRequestDict) impendingRequestDict.Remove(o1);
                            }, o);
                        // ReSharper restore MethodSupportsCancellation
#pragma warning restore 4014
                    }
                    else
                    {
                        lock (impendingRequestDict) impendingRequestDict.Remove(o);
                    }
                }, request.Id);
            }
            try
            {
                await OutBufferBlock.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Exception when sending the request…
                if (request != null)
                {
                    ctr.Dispose();
                    lock (impendingRequestDict) impendingRequestDict.Remove(request.Id);
                }
                throw;
            }
            if (request == null) return null;
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
    }
}
