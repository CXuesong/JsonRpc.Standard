using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Used to control the lifecycle of a JSON RPC service host.
    /// </summary>
    public interface IJsonRpcServiceHost
    {
        /// <summary>
        /// Asynchronously starts the JSON RPC service host.
        /// </summary>
        /// <param name="cancellationToken">The token used to shut down the service host.</param>
        /// <returns>A task that indicates the host state.</returns>
        /// <exception cref="InvalidOperationException">The service host is already running.</exception>
        Task RunAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Requests to stop the JSON RPC service host.
        /// </summary>
        /// <remarks>This method will do nothing if the service is not started.</remarks>
        void Stop();
    }

    /// <summary>
    /// Provides options for <see cref="JsonRpcServiceHost"/>.
    /// </summary>
    [Flags]
    public enum JsonRpcServiceHostOptions
    {
        None = 0,
        /// <summary>
        /// Makes the response sequence consistent with the request order.
        /// </summary>
        ConsistentResponseSequence
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
                var readerTask = Task.Factory.StartNew(state => ReaderEntryPoint((CancellationToken) state),
                    linked.Token, linked.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                var writerTask = Task.Factory.StartNew(state => WriterEntryPoint((CancellationToken) state),
                    linked.Token, linked.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                // Wait for Stop() or user cancellation.
                using (cancellationToken.Register(state => ((TaskCompletionSource<bool>) state).SetResult(true),
                    cancellationTcs))
                {
                    var finished = await Task.WhenAny(readerTask, cancellationTcs.Task);
                    if (finished == readerTask)
                    {
                        // Reader has reached EOF. Stop the server.
                        this.Stop();
                    }
                }
                // Wait some time for the writer task to finish.
                // await Task.WhenAny(writerTask.ContinueWith(_ => { }), Task.Delay(2000));
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
                var message = await Reader.ReadAsync(ct);
                if (message == null) return;        // EOF reached.
                try
                {
                    ct.ThrowIfCancellationRequested();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                var request = message as GeneralRequestMessage;
                if (request == null)
                {
                    // TODO log the error
                }
                else
                {
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
                        if (response == null) goto WAIT;
                        // Pick out the node
                        responseQueue.Remove(node);
                    }
                    // This operation might simply block the thread, rather than async.
                    await Writer.WriteAsync(response, ct);
                }
                WAIT:;
                try
                {
                    ct.ThrowIfCancellationRequested();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private async Task RpcMethodEntryPoint(object o)
        {
            var state = (RpcMethodEntryPointState) o;
            var method = Resolver.TryResolve(state.Context);
            ResponseMessage response = null;
            if (method == null)
            {
                if (state.Context.Request is RequestMessage request)
                {
                    response = new ResponseMessage(request.Id, null,
                        new ResponseError(JsonRpcErrorCode.MethodNotFound,
                            $"Cannot resolve method \"{request.Method}\""));
                }
            }
            else
            {
                response = await method.Invoker.InvokeAsync(method, state.Context);
            }
            try
            {
                state.Context.CancellationToken.ThrowIfCancellationRequested();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            if (response == null)
            {
                // Provides a default response
                if (state.Context.Request is RequestMessage request)
                {
                    response = new ResponseMessage(request.Id, null);
                }
            }
            lock (responseQueue)
            {
                state.ResponseSlot.Response = response;
            }
            try
            {
                state.Context.CancellationToken.ThrowIfCancellationRequested();
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
