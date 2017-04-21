using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard.Server
{
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
    /// Used to host a JSON RPC services.
    /// </summary>
    public class JsonRpcServiceHost : IJsonRpcServiceHost
    {
        private volatile CancellationTokenSource cts = null;
        private int isRunning = 0;
        //private readonly LinkedList<ResponseSlot> responseQueue = new LinkedList<ResponseSlot>();

        public JsonRpcServiceHost(MessageReader reader, MessageWriter writer, IRpcMethodResolver resolver)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            Reader = reader;
            Writer = writer;
            Resolver = resolver;
        }

        public MessageReader Reader { get; }

        public MessageWriter Writer { get; }

        public IRpcMethodResolver Resolver { get; }

        public ISession Session { get; }

        /// <summary>
        /// Asynchronously starts the JSON RPC service host.
        /// </summary>
        /// <param name="cancellationToken">The token used to shut down the service host.</param>
        /// <returns>A task that indicates the host state.</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var localRunning = Interlocked.Exchange(ref isRunning, 1);
            if (localRunning != 0) throw new InvalidOperationException("The service host is already running.");
            using (var cts = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
            {
                var task = Task.Factory.StartNew(state => ReaderEntryPoint((CancellationToken)state), linked.Token,
                    linked.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                await task;
            }
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

        private Task ReaderEntryPoint(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            while (true)
            {
                var message = Reader.Read();
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
                    if (context.Request is RequestMessage) responseSlot = new ResponseSlot();
                    Task.Factory.StartNew(RpcMethodEntryPoint, new RpcMethodEntryPointState(context, responseSlot),
                        context.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
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
            if (response == null)
            {
                // Provides a default response
                if (state.Context.Request is RequestMessage request)
                {
                    response = new ResponseMessage(request.Id, null);
                }
            }
            if (response != null)
            {
                //Writer.Write();
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
            private ResponseMessage _Response;
            private readonly object syncLock = new object();

            public ResponseMessage Response
            {
                get
                {
                    lock (syncLock) return _Response;
                }
                set
                {
                    lock (syncLock) _Response = value;
                }
            }
        }
    }
}
