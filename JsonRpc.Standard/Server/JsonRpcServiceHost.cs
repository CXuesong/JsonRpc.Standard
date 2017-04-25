using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
    public class JsonRpcServiceHost
    {
        public JsonRpcServiceHost(IRpcMethodResolver resolver, JsonRpcServiceHostOptions options)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            Resolver = resolver;
            Propagator = new TransformBlock<Message, ResponseMessage>(
                (Func<Message, Task<ResponseMessage>>) ReaderAction,
                new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = (options & JsonRpcServiceHostOptions.ConsistentResponseSequence) ==
                                    JsonRpcServiceHostOptions.ConsistentResponseSequence
                });
            Options = options;
        }

        protected IPropagatorBlock<Message, ResponseMessage> Propagator { get; }

        public IRpcMethodResolver Resolver { get; }

        public ISession Session { get; set; }

        public JsonRpcServiceHostOptions Options { get; }

        /// <summary>
        /// Attaches the host to the specific source block and target block.
        /// </summary>
        /// <param name="source">The source block used to retrieve the requests.</param>
        /// <param name="target">The target block used to emit the responses.</param>
        /// <returns>A <see cref="IDisposable"/> used to disconnect the source and target blocks.</returns>
        /// <exception cref="ArgumentNullException">Both <paramref name="source"/> and <paramref name="target"/> are <c>null</c>.</exception>
        public IDisposable Attach(ISourceBlock<Message> source, ITargetBlock<ResponseMessage> target)
        {
            if (source == null && target == null)
                throw new ArgumentNullException("Either source or target should not be null.", (Exception) null);
            IDisposable d1 = null;
            if (source != null)
            {
                d1 = source.LinkTo(Propagator, m => m is GeneralRequestMessage);
            }
            if (target != null)
            {
                var d2 = Propagator.LinkTo(target, m => m != null);
                if (d1 != null && d2 != null) return Utility.CombineDisposable(d1, d2);
                return d2;
            }
            return d1;
        }

        private Task<ResponseMessage> ReaderAction(Message message)
        {
            var ct = CancellationToken.None;
            if (ct.IsCancellationRequested) return Task.FromCanceled<ResponseMessage>(ct);
            var request = message as GeneralRequestMessage;
            if (request == null) return Task.FromResult<ResponseMessage>(null);
            // TODO provides a way to cancel the request from inside JsonRpcService.
            // TODO
            var context = new RequestContext(Session, request, ct);
            return RpcMethodEntryPoint(context);
        }

        private async Task<ResponseMessage> RpcMethodEntryPoint(RequestContext context)
        {
            var request = context.Request as RequestMessage;
            JsonRpcMethod method;
            try
            {
                method = Resolver.TryResolve(context);
            }
            catch (AmbiguousMatchException)
            {
                if (request != null)
                    return new ResponseMessage(request.Id, null, new ResponseError(JsonRpcErrorCode.MethodNotFound,
                        $"Invocation of method \"{request.Method}\" is ambiguous."));
                return null;
            }
            if (method == null)
            {
                if (request != null)
                    return new ResponseMessage(request.Id, null, new ResponseError(JsonRpcErrorCode.MethodNotFound,
                        $"Cannot resolve method \"{request.Method}\"."));
                return null;
            }
            var response = await method.Handler.InvokeAsync(method, context).ConfigureAwait(false);
            if (request != null && response == null)
            {
                // Provides a default response
                return new ResponseMessage(request.Id, null);
            }
            return response;
        }
    }
}
