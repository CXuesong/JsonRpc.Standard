using System;
using System.Collections;
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
    public interface IJsonRpcMethodHandler
    {
        /// <summary>
        /// Invokes the specified JSON RPC method asynchronously, using the specified request context.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="arguments">The arguments of the invocation. <c>null</c> for empty params.</param>
        /// <returns>
        /// A task that returns a <see cref="object"/> to indicate the response,
        /// or that returns <c>null</c> for the default response.
        /// </returns>
        /// <remarks>
        /// This method will usually called from a working thread.
        /// If there is error or exception occurred during invocation,
        /// it should be encapsulated in the <see cref="object"/>.
        /// </remarks>
        Task<object> InvokeAsync(RequestContext context,object[] arguments);
    }

    /// <summary>
    /// The default implementation of <see cref="IJsonRpcMethodHandler"/>.
    /// </summary>
    internal class ReflectionJsonRpcMethodHandler : IJsonRpcMethodHandler
    {
        private readonly Type serviceType;
        private readonly MethodInfo methodInfo;

        public ReflectionJsonRpcMethodHandler(Type serviceType, MethodInfo methodInfo)
        {
            this.serviceType = serviceType;
            this.methodInfo = methodInfo;
        }

        /// <inheritdoc />
        public async Task<object> InvokeAsync(RequestContext context, object[] arguments)
        {
            var inst = context.ServiceHost.ServiceFactory.CreateService(serviceType, context);
            inst.RequestContext = context;
            var result = methodInfo.Invoke(inst, arguments);
            if (result is Task taskResult)
            {
                // Wait for the task to complete.
                await taskResult;
                // Then collect result of the task.
                if (result.GetType().IsConstructedGenericType &&
                    result.GetType().GetGenericTypeDefinition() == typeof(Task<>))
                {
                    result = taskResult.GetType().GetRuntimeProperty("Result").GetValue(taskResult);
                }
            }
            context.ServiceHost.ServiceFactory.ReleaseService(inst);
            return result;
        }

    }
}
