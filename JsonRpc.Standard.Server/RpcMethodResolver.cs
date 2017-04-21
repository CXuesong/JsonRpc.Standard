using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JsonRpc.Standard.Server
{

    public class JsonRpcMethod
    {
        public JsonRpcMethod()
        {

        }

        public Type ServiceType { get; set; }

        public MethodInfo ServiceMethod { get; set; }

        public string MethodName { get; set; }

        internal static JsonRpcMethod FromMethod(Type type, MethodInfo method, bool camelCase)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (method == null) throw new ArgumentNullException(nameof(method));
            var inst = new JsonRpcMethod {ServiceType = type, ServiceMethod = method};
            var attr = method.GetCustomAttribute<JsonRpcMethodAttribute>();
            inst.MethodName = attr?.MethodName;
            if (inst.MethodName == null)
            {
                inst.MethodName = method.Name;
                if (camelCase)
                    inst.MethodName = char.ToLowerInvariant(inst.MethodName[0]) + inst.MethodName.Substring(1);
            }
            return inst;
        }
    }

    public interface IRpcMethodResolver
    {
        JsonRpcMethod TryResolve(GeneralRequestMessage request, ISession context);
    }

    public class RpcMethodResolver : IRpcMethodResolver
    {
        private readonly Dictionary<string, JsonRpcMethod> methodDict = new Dictionary<string, JsonRpcMethod>();

        public Assembly Assembly { get; }

        public RpcMethodResolver(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            Assembly = assembly;
        }

        public void Register(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (!typeof(JsonRpcService).GetTypeInfo().IsAssignableFrom(serviceType.GetTypeInfo()))
                throw new ArgumentException("serviceType is not a derived type from JsonRpcService.");
            if (serviceType.GetTypeInfo().IsAbstract)
                throw new ArgumentException("serviceType is abstract.");
            lock (methodDict)
            {
                foreach (var m in OnResolveMethods(serviceType))
                {
                    try
                    {
                        methodDict.Add(m.MethodName, m);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException($"A RPC method named {m.MethodName} already exists.");
                    }
                }
            }
        }

        /// <inheritdoc />
        public JsonRpcMethod TryResolve(GeneralRequestMessage request, ISession context)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            lock (methodDict)
            {
                if (methodDict.TryGetValue(request.Method, out var rpcm)) return rpcm;
            }
            return null;
        }

        protected virtual IEnumerable<JsonRpcMethod> OnResolveMethods(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            foreach (var m in serviceType.GetRuntimeMethods().Where(m => m.IsPublic && m.GetCustomAttribute<JsonRpcMethodAttribute>() != null))
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
