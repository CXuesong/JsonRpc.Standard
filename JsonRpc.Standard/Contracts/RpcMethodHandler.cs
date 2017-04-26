using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Defines method to invoke the specified JSON RPC method.
    /// </summary>
    public interface IRpcMethodHandler
    {
        /// <summary>
        /// Invokes the specified JSON RPC method asynchronously, using the specified request context.
        /// </summary>
        /// <param name="method">The method to be invoked.</param>
        /// <param name="context">The context of the invocation.</param>
        /// <returns>
        /// A task that returns a <see cref="ResponseMessage"/> to indicate the response,
        /// or that returns <c>null</c> for the default response.
        /// </returns>
        /// <remarks>
        /// This method will usually called from a working thread.
        /// If there is error or exception occurred during invocation,
        /// it should be encapsulated in the <see cref="ResponseMessage"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Either <paramref name="method"/> or <paramref name="context"/> is <c>null</c>.</exception>
        Task<ResponseMessage> InvokeAsync(JsonRpcMethod method, RequestContext context);
    }

    /// <summary>
    /// The default implementation of <see cref="IRpcMethodHandler"/>.
    /// </summary>
    internal class ReflectionRpcMethodHandler : IRpcMethodHandler
    {
        private readonly MethodInfo methodInfo;
        private readonly IList<ParameterInfo> args;

        public ReflectionRpcMethodHandler(MethodInfo methodInfo)
        {
            if (methodInfo == null) throw new ArgumentNullException(nameof(methodInfo));
            this.methodInfo = methodInfo;
            args = methodInfo.GetParameters();
        }

        private static readonly MethodInfo Task_FromResult = typeof(Task).GetTypeInfo()
            .GetDeclaredMethods("FromResult")
            .First(m => m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1);

        /// <inheritdoc />
        public async Task<ResponseMessage> InvokeAsync(JsonRpcMethod method, RequestContext context)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (method.Parameters.Count != args.Count)
                throw new InvalidOperationException($"Attempt to invoke a method that is not {methodInfo}.");
            var argv = new object[method.Parameters.Count];
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                // Resolve cancellation token
                if (method.Parameters[i].ParameterType == typeof(CancellationToken))
                {
                    argv[i] = context.CancellationToken;
                    continue;
                }
                // Resolve other parameters, considering the optional
                var jarg = context.Request.Parameters?[method.Parameters[i].ParameterName];
                if (jarg == null)
                {
                    if (method.Parameters[i].IsOptional)
                        argv[i] = Type.Missing;
                    else if (context.Request is RequestMessage request)
                        return new ResponseMessage(request.Id, new ResponseError(JsonRpcErrorCode.InvalidParams,
                            $"Required parameter \"{method.Parameters[i].ParameterName}\" is missing for \"{method.MethodName}\"."));
                    else
                    {
                        // TODO Logging: Argument missing, but the client do not need a response, so we just ignore the error.
                    }
                }
                else
                {
                    try
                    {
                        argv[i] = jarg.ToObject(method.Parameters[i].ParameterType, method.Parameters[i].Serializer);
                    }
                    catch (JsonException ex)
                    {
                        if (context.Request is RequestMessage request)
                            return new ResponseMessage(request.Id, new ResponseError(JsonRpcErrorCode.ParseError,
                                $"JSON error when parsing argument \"{method.Parameters[i].ParameterName}\" in \"{method.MethodName}\": {ex.Message}"));
                    }
                    if (method.Parameters[i].IsTask)
                    {
                        // This is a rare case, I suppose.
                        argv[i] = Task_FromResult.MakeGenericMethod(method.Parameters[i].ParameterType)
                            .Invoke(null, new[] {argv[i]});
                    }
                }
            }
            var inst = OnGetService(method, context);
            inst.RequestContext = context;
            var result = methodInfo.Invoke(inst, argv);
            var response = await ToResponseMessageAsync(result, method, context);
            // Some cleanup
            inst.RequestContext = null;
            return response;
        }

        /// <summary>
        /// Converts the return value of the RPC method to <see cref="ResponseMessage"/> asynchronously.
        /// </summary>
        /// <param name="invocationResult">The return value of target method.</param>
        /// <param name="method">The resolved RPC method.</param>
        /// <param name="context">The context of the invocation.</param>
        /// <returns>A task that returns a <see cref="ResponseMessage"/> to indicate the response,
        /// or that returns <c>null</c> if the request do not need any response.</returns>
        protected virtual async Task<ResponseMessage> ToResponseMessageAsync(object invocationResult, JsonRpcMethod method, RequestContext context)
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
                if (result == null)
                    return new ResponseMessage(request.Id, JValue.CreateNull());
                if (result is ResponseError error)
                    return new ResponseMessage(request.Id, error);
                return new ResponseMessage(request.Id, JToken.FromObject(result, method.ReturnParameter.Serializer));
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
