using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Provides information to map an argument in JSON RPC method to a CLR method argument.
    /// </summary>
    public class JsonRpcParameter
    {
        public string ParameterName { get; set; }

        public bool IsOptional { get; set; }

        internal static JsonRpcParameter FromParameter(ParameterInfo parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            var inst = new JsonRpcParameter {ParameterName = parameter.Name, IsOptional = parameter.IsOptional};
            return inst;
        }
    }

    /// <summary>
    /// Provides information to map a JSON RPC method to a CLR method.
    /// </summary>
    public class JsonRpcMethod
    {
        public JsonRpcMethod()
        {

        }

        public Type ServiceType { get; set; }

        public string MethodName { get; set; }

        public IList<JsonRpcParameter> Parameters { get; set; }

        public IRpcMethodInvoker Invoker { get; set; }

        internal static JsonRpcMethod FromMethod(Type type, MethodInfo method, bool camelCase)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (method == null) throw new ArgumentNullException(nameof(method));
            var inst = new JsonRpcMethod {ServiceType = type};
            var attr = method.GetCustomAttribute<JsonRpcMethodAttribute>();
            inst.MethodName = attr?.MethodName;
            if (inst.MethodName == null)
            {
                inst.MethodName = method.Name;
                if (camelCase)
                    inst.MethodName = char.ToLowerInvariant(inst.MethodName[0]) + inst.MethodName.Substring(1);
            }
            inst.Parameters = method.GetParameters().Select(JsonRpcParameter.FromParameter).ToList();
            inst.Invoker = new RpcMethodInvoker();
            return inst;
        }
    }

    /// <summary>
    /// Defines method to resolve the target RPC method from the JSON RPC request.
    /// </summary>
    public interface IRpcMethodResolver
    {
        /// <summary>
        /// Resolves the target RPC method from the JSON RPC request.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>Target RPC method information, or <c>null</c> if no such method exists.</returns>
        JsonRpcMethod TryResolve(RequestContext context);
    }

    public class RpcMethodResolver : IRpcMethodResolver
    {
        private readonly IDictionary<string, ICollection<JsonRpcMethod>> methodDict =
            new Dictionary<string, ICollection<JsonRpcMethod>>();

        public RpcMethodResolver()
        {

        }

        /// <summary>
        /// Registers all the exposed JSON PRC methods in the public service object types contained in the specified assembly.
        /// </summary>
        /// <param name="assembly">An assembly.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <c>null</c>.</exception>
        public void Register(Assembly assembly)
        {
            foreach (var t in assembly.ExportedTypes
                .Where(t => typeof(JsonRpcService).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo())))
                Register(t);
        }

        /// <summary>
        /// Registers all the exposed JSON PRC methods in the specified service object type.
        /// </summary>
        /// <param name="serviceType">A subtype of <see cref="JsonRpcService"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="serviceType"/> is not a derived type from <see cref="JsonRpcService"/>.</exception>
        public void Register(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (!typeof(JsonRpcService).GetTypeInfo().IsAssignableFrom(serviceType.GetTypeInfo()))
                throw new ArgumentException("serviceType is not a derived type from JsonRpcService.");
            // Maybe the RpcMethodInvoker can find a concrete subclass of this type somehow.
            //if (serviceType.GetTypeInfo().IsAbstract)
            //    throw new ArgumentException("serviceType is abstract.");
            lock (methodDict)
            {
                foreach (var m in OnResolveMethods(serviceType))
                {
                    try
                    {
                        ICollection<JsonRpcMethod> methods;
                        if (!methodDict.TryGetValue(m.MethodName, out methods))
                        {
                            methods = new List<JsonRpcMethod>();
                            methodDict.Add(m.MethodName, methods);
                        }
                        
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException($"A RPC method named {m.MethodName} already exists.");
                    }
                }
            }
        }

        /// <inheritdoc />
        public virtual JsonRpcMethod TryResolve(RequestContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            lock (methodDict)
            {
                if (methodDict.TryGetValue(context.Request.Method, out var rpcm))
                {
                    // Matches parameters
                    return TryResolve(context, rpcm);
                }
            }
            return null;
        }

        public virtual JsonRpcMethod TryResolve(RequestContext context, ICollection<JsonRpcMethod> candidates)
        {
            foreach (var m in candidates)
            {
                if (m.Parameters.All(p => p.IsOptional || context.Request.Params[p.ParameterName] != null))
                    return m;
            }
            return null;
        }

        /// <summary>
        /// Resolves all the RPC methods from the specified service type.
        /// </summary>
        /// <param name="serviceType"></param>
        /// <returns></returns>
        protected virtual IEnumerable<JsonRpcMethod> OnResolveMethods(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            foreach (var m in serviceType.GetRuntimeMethods().Where(m => m.GetCustomAttribute<JsonRpcMethodAttribute>() != null))
            {
                var rpcm = JsonRpcMethod.FromMethod(serviceType, m, true);
                yield return rpcm;
            }
        }

        protected virtual JsonRpcService OnGetInstance(Type serviceType, GeneralRequestMessage request)
        {
            return (JsonRpcService) Activator.CreateInstance(serviceType);
        }
    }
}
