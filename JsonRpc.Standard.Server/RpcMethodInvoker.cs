using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Defines method to invoke the specified JSON RPC method.
    /// </summary>
    public interface IRpcMethodInvoker
    {
        /// <summary>
        /// Invokes the specified JSON RPC method asynchronously, using the specified request context.
        /// </summary>
        /// <param name="method">The method to be invoked.</param>
        /// <param name="context">The context of the invocation.</param>
        /// <returns>A task that returns a <see cref="ResponseMessage"/> to indicate the response,
        /// or that returns <c>null</c> for the default response.</returns>
        /// <remarks>This method will usually called from a working thread.</remarks>
        /// <exception cref="ArgumentNullException">Either <paramref name="method"/> or <paramref name="context"/> is <c>null</c>.</exception>
        Task<ResponseMessage> InvokeAsync(JsonRpcMethod method, RequestContext context);
    }

    /// <summary>
    /// The default implementation of <see cref="IRpcMethodInvoker"/>.
    /// </summary>
    public class RpcMethodInvoker : IRpcMethodInvoker
    {
        /// <inheritdoc />
        public async Task<ResponseMessage> InvokeAsync(JsonRpcMethod method, RequestContext context)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var args = method.ServiceMethod.GetParameters();
            var argv = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                // Resolve cancellation token
                if (args[i].ParameterType == typeof(CancellationToken))
                {
                    argv[i] = context.CancellationToken;
                    continue;
                }
                // Resolve other parameters, considering the optional
                var jarg = context.Request.Params?[args[i].Name];
                if (jarg == null)
                {
                    if (args[i].IsOptional)
                        argv[i] = Type.Missing;
                    else if (context.Request is RequestMessage request)
                        return new ResponseMessage(request.Id, null, new ResponseError(JsonRpcErrorCode.InvalidParams,
                            $"Required parameter \"{args[i]}\" is missing for \"{method.MethodName}\"."));
                    else
                    {
                        // TODO Logging: Argument missing, but the client do not need a response, so we just ignore the error.
                    }
                }
                else
                {
                    try
                    {
                        argv[i] = jarg.ToObject(args[i].ParameterType, context.ServiceContext.JsonSerializer);
                    }
                    catch (JsonException ex)
                    {
                        if (context.Request is RequestMessage request)
                            return new ResponseMessage(request.Id, null, new ResponseError(JsonRpcErrorCode.ParseError,
                                $"JSON error when parsing argument \"{args[i]}\" in \"{method.MethodName}\": {ex.Message}"));
                    }
                }
            }
            var inst = OnGetService(method, context);
            var result = method.ServiceMethod.Invoke(inst, argv);
            return await ToResponseMessageAsync(result, context);
        }

        /// <summary>
        /// Converts the return value of the RPC method to <see cref="ResponseMessage"/> asynchronously.
        /// </summary>
        /// <param name="invocationResult">The return value of target method.</param>
        /// <param name="context">The context of the invocation.</param>
        /// <returns>A task that returns a <see cref="ResponseMessage"/> to indicate the response,
        /// or that returns <c>null</c> if the request do not need any response.</returns>
        protected virtual async Task<ResponseMessage> ToResponseMessageAsync(object invocationResult, RequestContext context)
        {
            object result = null;
            if (invocationResult is Task taskResult)
            {
                // Wait for the task to complete.
                await taskResult;
                // Then collect result of the task.
                if (invocationResult.GetType().IsConstructedGenericType &&
                    invocationResult.GetType().GetGenericTypeDefinition() == typeof(Task<>))
                {
                    result = taskResult.GetType().GetRuntimeProperty("Result").GetValue(taskResult);
                }
            }
            else
            {
                result = invocationResult;
            }
            if (context.Request is RequestMessage request)
            {
                // We need a response.
                if (result is ResponseError error)
                    return new ResponseMessage(request.Id, null, error);
                return new ResponseMessage(request.Id, result);
            }
            // Otherwise, we do not send anything.
            return null;
        }

        /// <summary>
        /// Gets or creates a <see cref="JsonRpcService"/> instance that is used for JSON RPC invocation.
        /// </summary>
        /// <param name="method">The method to be invoked.</param>
        /// <param name="context">The context of the invocation.</param>
        /// <returns>A <see cref="JsonRpcService"/> that the specified method is to be invoked on.</returns>
        /// <remarks>
        /// The default implementation always instantiates a new instance of type specified in
        /// <paramref name="method"/> parameter using the public empty constructor.
        /// </remarks>
        protected virtual JsonRpcService OnGetService(JsonRpcMethod method, RequestContext context)
        {
            var service = Activator.CreateInstance(method.ServiceType);
            return (JsonRpcService) service;
        }
    }
}
