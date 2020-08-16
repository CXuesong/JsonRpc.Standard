using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using JsonRpc.Server;

namespace JsonRpc.Contracts
{
    /// <summary>
    /// Provides methods that can build <see cref="JsonRpcServerContract"/> for service and client.
    /// </summary>
    public interface IJsonRpcContractResolver
    {
        /// <summary>
        /// Builds a <see cref="JsonRpcServerContract"/> out of the specified JSON RPC service types.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="contractTypes"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">There is <c>null</c> element in <paramref name="contractTypes"/>.</exception>
        JsonRpcServerContract CreateServerContract(IEnumerable<Type> contractTypes);

        /// <summary>
        /// Builds a <see cref="JsonRpcServerContract"/> out of the specified JSON RPC client types.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="contractTypes"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">There is <c>null</c> element in <paramref name="contractTypes"/>.</exception>
        JsonRpcClientContract CreateClientContract(IEnumerable<Type> contractTypes);
    }

    /// <summary>
    /// A default implementation of <see cref="IJsonRpcContractResolver"/>.
    /// </summary>
    public class JsonRpcContractResolver : IJsonRpcContractResolver
    {
        private IJsonValueConverter _ParameterValueConverter = JsonValueConverter.Default;
        private JsonRpcNamingStrategy _NamingStrategy = JsonRpcNamingStrategy.Default;

        internal static readonly JsonRpcContractResolver Default = new JsonRpcContractResolver();

        /// <summary>
        /// Gets/sets the converter used to convert the arguments from and results to JSON.
        /// </summary>
        public IJsonValueConverter ParameterValueConverter
        {
            get => _ParameterValueConverter;
            set => _ParameterValueConverter = value ?? JsonValueConverter.Default;
        }

        /// <summary>
        /// Gets/sets the naming strategy used to map the RPC method and argument names.
        /// </summary>
        public JsonRpcNamingStrategy NamingStrategy
        {
            get => _NamingStrategy;
            set => _NamingStrategy = value ?? JsonRpcNamingStrategy.Default;
        }

        /// <inheritdoc />
        public JsonRpcServerContract CreateServerContract(IEnumerable<Type> contractTypes)
        {
            if (contractTypes == null) throw new ArgumentNullException(nameof(contractTypes));
            var contract = new JsonRpcServerContract();
            foreach (var t in contractTypes)
            {
                if (t == null) throw new ArgumentException("Sequence cannot contain null elements.", nameof(contractTypes));
                foreach (var p in MethodsFromType(t))
                {
                    if (!contract.Methods.TryGetValue(p.Value.MethodName, out var methodList))
                    {
                        methodList = new List<JsonRpcMethod>();
                        contract.Methods.Add(p.Value.MethodName, methodList);
                    }
                    methodList.Add(p.Value);
                }
            }
            return contract;
        }

        /// <inheritdoc />
        public JsonRpcClientContract CreateClientContract(IEnumerable<Type> contractTypes)
        {
            if (contractTypes == null) throw new ArgumentNullException(nameof(contractTypes));
            var contract = new JsonRpcClientContract();
            foreach (var t in contractTypes)
            {
                if (t == null) throw new ArgumentException("Sequence cannot contain null elements.", nameof(contractTypes));
                foreach (var m in MethodsFromType(t))
                {
                    contract.Methods[m.Key] = m.Value;
                }
            }
            return contract;
        }

        /// <summary>
        /// Gets a list of all the exposed JSON PRC methods in the specified service object type.
        /// </summary>
        /// <param name="serviceType">A subtype of <see cref="JsonRpcService"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="serviceType"/> is not a derived type from <see cref="JsonRpcService"/>.</exception>
        protected virtual IEnumerable<KeyValuePair<MethodInfo, JsonRpcMethod>> MethodsFromType(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            return serviceType.GetRuntimeMethods()
                .Select(m => new KeyValuePair<MethodInfo, JsonRpcMethod>(m, CreateMethod(serviceType, m)))
                .Where(p => p.Value != null);
        }

        protected virtual JsonRpcMethod CreateMethod(Type serviceType, MethodInfo method)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (method == null) throw new ArgumentNullException(nameof(method));
            var attr = CachedAttributeAccessor<JsonRpcMethodAttribute>.Get(method);
            if (attr == null) return null;

            Debug.Assert(serviceType.GetTypeInfo().IsAssignableFrom(method.DeclaringType.GetTypeInfo()));
            var inst = new JsonRpcMethod
            {
                ServiceType = serviceType,
                MethodName = attr.MethodName == null
                    ? NamingStrategy.GetRpcMethodName(method.Name, false)
                    : NamingStrategy.GetRpcMethodName(attr.MethodName, true)
            };

            var scope = CachedAttributeAccessor<JsonRpcScopeAttribute>.Get(serviceType);
            if (scope?.MethodPrefix != null)
                inst.MethodName = scope.MethodPrefix + inst.MethodName;
            inst.IsNotification = attr.IsNotification;
            inst.AllowExtensionData = attr.AllowExtensionData;
            inst.ReturnParameter = CreateParameter(serviceType, method.ReturnParameter, attr, scope);
            // Even void-type method has its ReturnParameter.
            Debug.Assert(inst.ReturnParameter != null, "CreateParameter should not return null.");
            inst.Parameters = method.GetParameters()
                .Select(p => CreateParameter(serviceType, p, attr, scope))
                .ToList();
            Debug.Assert(inst.Parameters.IndexOf(null) < 0, "CreateParameter should not return null.");
            inst.Invoker = new ReflectionJsonRpcMethodInvoker(serviceType, method);
            return inst;
        }

        /// <summary>
        /// Creates the <see cref="JsonRpcParameter"/> contract with the specified CLR parameter info.
        /// </summary>
        /// <param name="serviceType">Type of the JSON RPC service object.</param>
        /// <param name="parameter">CLR parameter object.</param>
        /// <param name="methodAttribute">Custom attribute applied to the CLR method, or <c>null</c> if none applied.</param>
        /// <param name="scopeAttribute">Custom attribute applied to the JSON RPC scope, or <c>null</c> if none applied.</param>
        protected virtual JsonRpcParameter CreateParameter(Type serviceType, ParameterInfo parameter,
            JsonRpcMethodAttribute methodAttribute, JsonRpcScopeAttribute scopeAttribute)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (parameter.IsOut || parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer)
                throw new NotSupportedException("Argument with out, ref, or of pointer type is not supported.");
            // parameter.Name == null for return parameter
            // Mono has a bug where parameter.Name == "" instead of null.
            // See https://github.com/CXuesong/JsonRpc.Standard/pull/9
            var isReturnParam = string.IsNullOrEmpty(parameter.Name);
            var taskResultType = Utility.GetTaskResultType(parameter.ParameterType);
            if (!isReturnParam && taskResultType != null)
                throw new NotSupportedException("Argument with type of System.Threading.Task is not supported.");
            // TODO bypass return value attribute check ONLY on Mono with FrameworkDescription check.
            var attr = isReturnParam ? null : CachedAttributeAccessor<JsonRpcParameterAttribute>.Get(parameter);
            var inst = new JsonRpcParameter
            {
                IsOptional = attr?.IsOptional ?? parameter.IsOptional,
                ParameterType = parameter.ParameterType,
                Converter = attr?.GetValueConverter() ?? methodAttribute?.GetValueConverter()
                            ?? scopeAttribute?.GetValueConverter() ?? ParameterValueConverter
            };
            if (inst.IsOptional)
            {
                inst.DefaultValue = attr?.IsOptional == true ? attr.DefaultValue : parameter.DefaultValue;
            }
            var localNamingStrategy = methodAttribute?.GetNamingStrategy() ?? scopeAttribute?.GetNamingStrategy()
                                      ?? NamingStrategy;
            if (attr?.ParameterName == null)
                inst.ParameterName = localNamingStrategy.GetRpcParameterName(parameter.Name, false);
            else
                inst.ParameterName = localNamingStrategy.GetRpcParameterName(attr.ParameterName, true);
            if (taskResultType != null)
            {
                if (Utility.GetTaskResultType(taskResultType) != null)
                    throw new NotSupportedException("Argument of nested Task type is not supported.");
                inst.ParameterType = taskResultType;
                inst.IsTask = true;
            }
            // This argument will always be injected by the invoker,
            // so we don't want it to interfere with method resolution.
            // TODO Take care of CancellationToken in method binders.
            if (inst.ParameterType == typeof(CancellationToken)) inst.IsOptional = true;
            return inst;
        }

        private static class CachedAttributeAccessor<T> where T : Attribute
        {

            private static readonly ConditionalWeakTable<object, T> cache = new ConditionalWeakTable<object, T>();

#if !BCL_FEATURE_TYPE_IS_MEMBER_INFO
            public static T Get(Type type) => Get(type.GetTypeInfo());
#endif

            private static T GetDirect(MemberInfo memberInfo)
            {
                var attr = memberInfo.GetCustomAttribute<T>();
                if (attr != null) return attr;
                if (memberInfo.DeclaringType == null) return null;

                // If we cannot find attribute on the class method, we check whether this method is implementing an interface with [T].
                var ownerType = memberInfo.DeclaringType;
                if (ownerType == null) return null;
#if BCL_FEATURE_TYPE_IS_MEMBER_INFO
                foreach (var interfaceType in ownerType.GetInterfaces())
                {
                    var map = ownerType.GetInterfaceMap(interfaceType);
#else
                foreach (var interfaceType in ownerType.GetTypeInfo().ImplementedInterfaces)
                {
                    var map = ownerType.GetTypeInfo().GetRuntimeInterfaceMap(interfaceType);
#endif
                    var index = Array.IndexOf(map.TargetMethods, memberInfo);
                    if (index >= 0)
                    {
                        var interfaceMethod = map.InterfaceMethods[index];
                        return interfaceMethod.GetCustomAttribute<T>();
                    }
                }
                return null;
            }

            public static T Get(MemberInfo memberInfo)
            {
                if (cache.TryGetValue(memberInfo, out var attr))
                    return attr;
                attr = GetDirect(memberInfo);
#if BCL_FEATURE_CWT_ADD_OR_UPDATE
                cache.AddOrUpdate(memberInfo, attr);
#else
                try
                {
                    cache.Add(memberInfo, attr);
                }
                catch (ArgumentException)
                {
                    // Conflicting key.
                }
#endif
                return attr;
            }

            public static T Get(ParameterInfo paramInfo)
            {
                if (cache.TryGetValue(paramInfo, out var attr))
                    return attr;
                attr = paramInfo.GetCustomAttribute<T>();
#if BCL_FEATURE_CWT_ADD_OR_UPDATE
                cache.AddOrUpdate(paramInfo, attr);
#else
                try
                {
                    cache.Add(paramInfo, attr);
                }
                catch (ArgumentException)
                {
                    // Conflicting key.
                }
#endif
                return attr;
            }

        }
    }
}
