using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using JsonRpc.Standard.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Provides information to map an argument in JSON RPC method to a CLR method argument.
    /// </summary>
    public class JsonRpcParameter
    {
        private static readonly JsonSerializer defaultSerializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private JsonSerializer _Serializer = defaultSerializer;

        /// <summary>
        /// The parameter name used in JSON.
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Whether the parameter is optional.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Whether the parameter is a Task or Task&lt;ParameterType&gt; instead of ParameterType itself.
        /// </summary>
        public bool IsTask { get; set; }

        /// <summary>
        /// The bare type of the parameter.
        /// </summary>
        public Type ParameterType { get; set; }

        /// <summary>
        /// The serializer used to convert the parameter.
        /// </summary>
        public JsonSerializer Serializer
        {
            get { return _Serializer; }
            set { _Serializer = value ?? defaultSerializer; }
        }

        /// <inheritdoc />
        public override string ToString() => ParameterType + " " + ParameterName;

        internal static JsonRpcParameter FromParameter(ParameterInfo parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            var inst = new JsonRpcParameter
            {
                ParameterName = parameter.Name,
                IsOptional = parameter.IsOptional,
                ParameterType = parameter.ParameterType
            };
            var taskResultType = Utility.GetTaskResultType(parameter.ParameterType);
            if (taskResultType != null)
            {
                inst.ParameterType = taskResultType;
                inst.IsTask = true;
            }
            // This argument will always be injected by the invoker,
            // so we don't want it to interfere with method resolution.
            if (inst.ParameterType == typeof(CancellationToken)) inst.IsOptional = true;
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

        public bool IsNotification { get; set; }

        public IList<JsonRpcParameter> Parameters { get; set; }

        public JsonRpcParameter ReturnParameter { get; set; }

        public IRpcMethodHandler Handler { get; set; }

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
            inst.IsNotification = attr?.IsNotification ?? false;
            inst.ReturnParameter = JsonRpcParameter.FromParameter(method.ReturnParameter);
            inst.Parameters = method.GetParameters().Select(JsonRpcParameter.FromParameter).ToList();
            inst.Handler = new ReflectionRpcMethodHandler(method);
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

    /// <summary>
    /// A lookup-table based <see cref="IRpcMethodResolver"/> implementation.
    /// </summary>
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
                        methods.Add(m);
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
                if (m.Parameters.All(p => p.IsOptional || context.Request.Params?[p.ParameterName] != null))
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
