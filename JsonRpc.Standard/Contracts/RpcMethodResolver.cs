using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using JsonRpc.Standard.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        /// <remarks>
        /// Parameters with certain types (e.g. <see cref="CancellationToken"/>)
        /// are always treated as optional.
        /// </remarks>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Whether the parameter is a Task or Task&lt;ParameterType&gt; instead of ParameterType itself.
        /// </summary>
        public bool IsTask { get; set; }

        /// <summary>
        /// The bare type of the parameter.
        /// </summary>
        public Type ParameterType { get; set; }

        internal bool MatchJTokenType(JTokenType type)
        {
            if (ParameterType == typeof(JToken)) return true;
            var ti = ParameterType.GetTypeInfo();
            switch (type)
            {
                case JTokenType.Object:
                    return !(ti.IsPrimitive || ParameterType == typeof(string));
                case JTokenType.Array:
                    return typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(ti);
                case JTokenType.Boolean:
                    return ParameterType == typeof(bool) || ParameterType == typeof(bool?);
                case JTokenType.Integer:
                case JTokenType.Float:
                    return ParameterType == typeof(byte) || ParameterType == typeof(byte?)
                           || ParameterType == typeof(short) || ParameterType == typeof(short?)
                           || ParameterType == typeof(int) || ParameterType == typeof(int?)
                           || ParameterType == typeof(long) || ParameterType == typeof(long?)
                           || ParameterType == typeof(sbyte) || ParameterType == typeof(sbyte?)
                           || ParameterType == typeof(ushort) || ParameterType == typeof(ushort?)
                           || ParameterType == typeof(uint) || ParameterType == typeof(uint?)
                           || ParameterType == typeof(ulong) || ParameterType == typeof(ulong?)
                           || ParameterType == typeof(float) || ParameterType == typeof(float?)
                           || ParameterType == typeof(double) || ParameterType == typeof(double?);
                case JTokenType.String:
                    return !ti.IsPrimitive || ParameterType == typeof(char) || ParameterType == typeof(char?);
                case JTokenType.Null:
                    return !ParameterType.GetTypeInfo().IsValueType
                           || ParameterType.IsConstructedGenericType &&
                           ParameterType.GetGenericTypeDefinition() == typeof(Nullable<>);
                default:
                    return false;
            }
        }

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
            if (parameter.IsOut || parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer)
                throw new NotSupportedException("Argument with out, ref, or of pointer type is not supported.");
            var inst = new JsonRpcParameter
            {
                ParameterName = parameter.Name,
                IsOptional = parameter.IsOptional,
                ParameterType = parameter.ParameterType
            };
            var taskResultType = Utility.GetTaskResultType(parameter.ParameterType);
            if (taskResultType != null)
            {
                if (Utility.GetTaskResultType(taskResultType) != null)
                    throw new NotSupportedException("Argument of nested Task type is not supported.");
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

        public bool AllowExtensionData { get; set; }

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
            inst.AllowExtensionData = attr?.AllowExtensionData ?? false;
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
        /// <returns>Target RPC method information, or <c>null</c> if no suitable method exists.</returns>
        /// <exception cref="AmbiguousMatchException">More than one method is found with that suits the specified request.</exception>
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

        /// <summary>
        /// Try to resolve a method from a sequence of methods with the same method names.
        /// </summary>
        /// <returns>The methods will be resolved by matching the type of parameters.</returns>
        public virtual JsonRpcMethod TryResolve(RequestContext context, ICollection<JsonRpcMethod> candidates)
        {
            //TODO Support array as params
            if (context.Request.Parameters != null && context.Request.Parameters.Type == JTokenType.Array) return null;
            JsonRpcMethod firstMatch = null;
            Dictionary<string, JToken> requestProp = null;
            foreach (var m in candidates)
            {
                if (!m.AllowExtensionData && context.Request.Parameters != null)
                {
                    // Strict match
                    requestProp = ((JObject) context.Request.Parameters).Properties()
                        .ToDictionary(p => p.Name, p => p.Value);
                }
                foreach (var p in m.Parameters)
                {
                    var jp = context.Request.Parameters?[p.ParameterName];
                    if (jp == null)
                    {
                        if (!p.IsOptional) goto NEXT;
                        else continue;
                    }
                    if (!p.MatchJTokenType(jp.Type)) goto NEXT;
                    requestProp?.Remove(p.ParameterName);
                }
                // Check whether we have extra parameters.
                if (requestProp != null && requestProp.Count > 0) goto NEXT;
                if (firstMatch != null) throw new AmbiguousMatchException();
                firstMatch = m;
                NEXT:
                ;
            }
            return firstMatch;
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
