using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Contracts;
using JsonRpc.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Server
{

    internal class JsonRpcServiceHost : IJsonRpcServiceHost
    {

        internal JsonRpcServiceHost(JsonRpcServerContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            Contract = contract;
        }

        internal JsonRpcServerContract Contract { get; }

        public IServiceFactory ServiceFactory { get; set; }

        internal IJsonRpcMethodBinder MethodBinder { get; set; }

        private RequestHandler pipeline;

        internal ILogger Logger { get; set; }

        // Middlewares, from innermost to outermost ones.
        internal void BuildPipeline(IEnumerable<Func<RequestHandler, RequestHandler>> middlewares)
        {
            RequestHandler handler = DispatchRpcMethod;
            foreach (var mw in middlewares) handler = mw(handler);
            pipeline = handler;
        }

        /// <inheritdoc />
        public async Task<ResponseMessage> InvokeAsync(RequestMessage request, IFeatureCollection features, CancellationToken cancellationToken)
        {
            var context = new RequestContext(this, ServiceFactory, new FeatureCollection(features),
                request, cancellationToken);
            try
            {
                await pipeline(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Swallow any exceptions
                Logger.LogError(1000, ex, "Unhandled exception while processing the request.\r\n{exception}", ex);
                if (context.Response != null)
                {
                    context.Response.Result = null;
                    context.Response.Error = ResponseError.FromException(ex, true);
                }
            }
            return context.Response;
        }

        private void TrySetErrorResponse(RequestContext context, JsonRpcErrorCode errorCode,
            string message)
        {
            Logger.LogError("({code}) {message}", errorCode, message);
            if (context.Response == null) return;
            context.Response.Error = new ResponseError(errorCode, message);
        }

        private bool ValidateRequest(RequestContext context)
        {
            if (context.Request.Method == null)
            {
                // Even "method": null is still allowed.
                TrySetErrorResponse(context, JsonRpcErrorCode.InvalidRequest,
                    "\"method\" property is missing in the request.");
                return false;
            }
            if (context.Request.Parameters is JValue jv)
            {
                if (jv.Type != JTokenType.Null && jv.Type != JTokenType.Undefined)
                {
                    TrySetErrorResponse(context, JsonRpcErrorCode.InvalidRequest,
                        "Invalid \"params\" value.");
                    return false;
                }
            }
            return true;
        }

        private async Task DispatchRpcMethod(RequestContext context)
        {
            if (!ValidateRequest(context)) return;
            JsonRpcMethod method;
            try
            {
                if (Contract.Methods.TryGetValue(context.Request.Method, out var candidates))
                {
                    method = MethodBinder.TryBindToMethod(candidates, context);
                }
                else
                {
                    TrySetErrorResponse(context, JsonRpcErrorCode.MethodNotFound,
                        $"Method \"{context.Request.Method}\" is not found.");
                    return;
                }
            }
            catch (AmbiguousMatchException)
            {
                TrySetErrorResponse(context, JsonRpcErrorCode.InvalidParams,
                    $"Invocation of method \"{context.Request.Method}\" is ambiguous.");
                return;
            }
            if (method == null)
            {
                TrySetErrorResponse(context, JsonRpcErrorCode.InvalidParams,
                    $"Cannot find method \"{context.Request.Method}\" with matching signature.");
                return;
            }
            // Parse the arguments
            object[] args;
            try
            {
                args = MethodBinder.BindParameters(method.Parameters, context);
            }
            catch (Exception ex)
            {
                TrySetErrorResponse(context, JsonRpcErrorCode.InvalidParams, ex.Message);
                if (context.Response != null)
                    context.Response.Error = ResponseError.FromException(ex, true);
                return;
            }
            // Call the method
            try
            {
                var result = await method.Invoker.InvokeAsync(context, args).ConfigureAwait(false);
                // Produce the response.
                if (context.Response != null && context.Response.Result == null && context.Response.Error == null)
                {
                    if (result is ResponseError err)
                        context.Response.Error = err;
                    else
                    {
                        context.Response.Result = method.ReturnParameter.Converter.ValueToJson(result);
                        // JValue(null) is not `null`
                        Debug.Assert(context.Response.Result != null);
                    }
                }
            }
            catch (JsonRpcException ex)
            {
                if (context.Response != null)
                {
                    context.Response.Result = null;
                    context.Response.Error = ex.Error;
                }
            }
            // Let other exceptions bubble up. We will handle them in the pipe, or finally in InvokePipeline.
        }
    }
}

