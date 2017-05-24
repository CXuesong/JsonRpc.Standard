using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;

namespace JsonRpc.Dataflow
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
    /// Transmits JSON RPC requests via TPL Dataflow blocks.
    /// </summary>
    public class DataflowRpcClientHandler : JsonRpcClientHandler
    {

        private readonly Dictionary<MessageId, TaskCompletionSource<ResponseMessage>> impendingRequestDict
            = new Dictionary<MessageId, TaskCompletionSource<ResponseMessage>>();

        public DataflowRpcClientHandler() : this(JsonRpcClientOptions.None)
        {
        }

        public DataflowRpcClientHandler(JsonRpcClientOptions options)
        {
            Options = options;
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

        /// <inheritdoc />
        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<ResponseMessage> tcs = null;
            CancellationTokenRegistration ctr = default(CancellationTokenRegistration);
            if (!request.IsNotification)
            {
                // We need to monitor the response.
                tcs = new TaskCompletionSource<ResponseMessage>();
                lock (impendingRequestDict) impendingRequestDict.Add(request.Id, tcs);
                ctr = cancellationToken.Register(o =>
                {
                    TaskCompletionSource<ResponseMessage> tcs1;
                    var id = (MessageId)o;
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
                                lock (impendingRequestDict) impendingRequestDict.Remove((MessageId)o1);
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
            Debug.Assert(tcs != null);
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
    }
}
