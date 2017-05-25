using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    public interface IJsonRpcMethodInvoker
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
        /// <exception cref="Exception">The invoked method throws an exception.</exception>
        Task<object> InvokeAsync(RequestContext context, object[] arguments);
    }

    /// <summary>
    /// The default implementation of <see cref="IJsonRpcMethodInvoker"/>.
    /// </summary>
    internal class ReflectionJsonRpcMethodInvoker : IJsonRpcMethodInvoker
    {
        private readonly Type serviceType;
        private readonly MethodInfo methodInfo;

        public ReflectionJsonRpcMethodInvoker(Type serviceType, MethodInfo methodInfo)
        {
            this.serviceType = serviceType;
            this.methodInfo = methodInfo;
        }

        /// <inheritdoc />
        public async Task<object> InvokeAsync(RequestContext context, object[] arguments)
        {
            var inst = context.ServiceFactory.CreateService(serviceType, context);
            try
            {
                inst.RequestContext = context;
                object result;
                try
                {
                    result = methodInfo.Invoke(inst, arguments);
                }
                catch (TargetInvocationException ex)
                {
                    // Expand the actual exception.
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    // We should never reach here.
                    throw;
                }
                if (result is Task taskResult)
                {
                    // Wait for the task to complete.
                    await taskResult;
                    // Then collect the result of the task.
                    var resultMethod = taskResult.GetType().GetRuntimeProperty("Result");
                    result = resultMethod?.GetValue(taskResult);
                }
                return result;
            }
            finally
            {
                inst.RequestContext = null;
                context.ServiceFactory.ReleaseService(inst);
            }
        }
    }
}
