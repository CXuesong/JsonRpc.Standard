using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard.Contracts;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Provides options for <see cref="JsonRpcServiceHost"/>.
    /// </summary>
    [Flags]
    public enum JsonRpcServiceHostOptions
    {
        /// <summary>
        /// No options.
        /// </summary>
        None = 0,
        /// <summary>
        /// Makes the response sequence consistent with the request order.
        /// </summary>
        ConsistentResponseSequence,
    }

    /// <summary>
    /// Used to host a JSON RPC services.
    /// </summary>
    public class JsonRpcServiceHost : IJsonRpcServiceHost
    {
        private volatile CancellationTokenSource cts = null;
        private int isRunning = 0;
        private volatile SemaphoreSlim responseQueueSemaphore = null;
        private readonly LinkedList<ResponseSlot> responseQueue = new LinkedList<ResponseSlot>();

        public JsonRpcServiceHost(MessageReader reader, MessageWriter writer,
            IRpcMethodResolver resolver, JsonRpcServiceHostOptions options)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            Reader = reader;
            Writer = writer;
            Resolver = resolver;
            Options = options;
        }

        public MessageReader Reader { get; }

        public MessageWriter Writer { get; }

        public IRpcMethodResolver Resolver { get; }

        public ISession Session { get; set; }

        public JsonRpcServiceHostOptions Options { get; }

        /// <summary>
        /// Asynchronously starts the JSON RPC service host.
        /// </summary>
        /// <param name="cancellationToken">The token used to shut down the service host.</param>
        /// <returns>A task that indicates the host state.</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var localRunning = Interlocked.Exchange(ref isRunning, 1);
            if (localRunning != 0) throw new InvalidOperationException("The service host is already running.");
            using (responseQueueSemaphore = new SemaphoreSlim(0))
            using (cts = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
            {
                var cancellationTcs = new TaskCompletionSource<bool>();
                var writerTask = Task.Factory.StartNew(state => WriterEntryPoint((CancellationToken) state).GetAwaiter().GetResult(),
                    linked.Token, linked.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                var readerTask = Task.Factory.StartNew(state => ReaderEntryPoint((CancellationToken) state).GetAwaiter().GetResult(),
                    linked.Token, linked.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                // Tasks that we can safely await
                var readerTask1 = readerTask.ContinueWith(_ => { });
                var writerTask1 = writerTask.ContinueWith(_ => { });
                // Wait for reader EOF, Stop(), or user cancellation.
                await Task.WhenAny(readerTask1, cancellationTcs.Task);
                // Cleanup.
                lock (responseQueue) responseQueue.Clear();
            }
            Interlocked.Exchange(ref isRunning, 0);
        }

        /// <inheritdoc />
        public void Stop()
        {
            try
            {
                var localCts = cts;
                localCts?.Cancel(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task ReaderEntryPoint(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var request = (GeneralRequestMessage) await Reader.ReadAsync(m => m is GeneralRequestMessage, ct);
                if (request == null) return; // EOF reached.
                try
                {
                    ct.ThrowIfCancellationRequested();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                // TODO provides a way to cancel the request from inside JsonRpcService.
                var context = new RequestContext(this, Session, request, ct);
                ResponseSlot responseSlot = null;
                if (context.Request is RequestMessage)
                {
                    responseSlot = new ResponseSlot();
                    lock (responseQueue)
                    {
                        responseQueue.AddLast(responseSlot);
                    }
                }
#pragma warning disable 4014
                Task.Factory.StartNew(RpcMethodEntryPoint, new RpcMethodEntryPointState(context, responseSlot),
                    context.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
#pragma warning restore 4014
            }
        }

        private async Task WriterEntryPoint(CancellationToken ct)
        {
            var preserveOrder = (Options & JsonRpcServiceHostOptions.ConsistentResponseSequence) ==
                                JsonRpcServiceHostOptions.ConsistentResponseSequence;
            while (true)
            {
                // Wait for incoming response, or cancellation.
                try
                {
                    ct.ThrowIfCancellationRequested();
                    await responseQueueSemaphore.WaitAsync(ct);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                while (true)
                {
                    ResponseMessage response;
                    lock (responseQueue)
                    {
                        var node = responseQueue.First;
                        if (!preserveOrder)
                        {
                            while (node != null)
                            {
                                if (node.Value.Response != null) break;
                                node = node.Next;
                            }
                        }
                        response = node?.Value.Response;
                        // If the response is not ready, then we simply wait for the next "response ready" event.
                        if (response == null) goto NEXT;
                        // Pick out the node
                        responseQueue.Remove(node);
                    }
                    // This operation might simply block the thread, rather than async.
                    await Writer.WriteAsync(response, ct);
                }
                NEXT:;
            }
        }

        private async Task RpcMethodEntryPoint(object o)
        {
            var state = (RpcMethodEntryPointState) o;
            var request = state.Context.Request as RequestMessage;
            ResponseMessage response = null;
            JsonRpcMethod method;
            try
            {
                method = Resolver.TryResolve(state.Context);
            }
            catch (AmbiguousMatchException)
            {
                if (request != null)
                    response = new ResponseMessage(request.Id, null, new ResponseError(JsonRpcErrorCode.MethodNotFound,
                        $"Invocation of method \"{request.Method}\" is ambiguous."));
                goto FINAL;
            }
            if (method == null)
            {
                if (request != null)
                    response = new ResponseMessage(request.Id, null, new ResponseError(JsonRpcErrorCode.MethodNotFound,
                        $"Cannot resolve method \"{request.Method}\"."));
                goto FINAL;
            }
            response = await method.Handler.InvokeAsync(method, state.Context);
            FINAL:
            if (request != null && response == null)
            {
                // Provides a default response
                response = new ResponseMessage(request.Id, null);
            }
            if (response != null)
            {
                lock (responseQueue) state.ResponseSlot.Response = response;
            }
            try
            {
                responseQueueSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }

        private class RpcMethodEntryPointState
        {
            public RpcMethodEntryPointState(RequestContext context, ResponseSlot responseSlot)
            {
                Context = context;
                ResponseSlot = responseSlot;
            }

            public RequestContext Context { get; }

            public ResponseSlot ResponseSlot { get; }
        }

        private class ResponseSlot
        {
            public ResponseMessage Response { get; set; }
        }
    }
}
