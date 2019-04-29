using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            var scope = serviceType.GetTypeInfo().GetCustomAttribute<JsonRpcScopeAttribute>();
            return serviceType.GetRuntimeMethods()
                .Where(m => m.GetCustomAttribute<JsonRpcMethodAttribute>() != null)
                .Select(m => new KeyValuePair<MethodInfo, JsonRpcMethod>(m, CreateMethod(serviceType, m, scope)));
        }
        
        protected virtual JsonRpcMethod CreateMethod(Type serviceType, MethodInfo method, JsonRpcScopeAttribute scopeAttribute)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (method == null) throw new ArgumentNullException(nameof(method));
            var inst = new JsonRpcMethod {ServiceType = serviceType};
            var attr = method.GetCustomAttribute<JsonRpcMethodAttribute>();
            inst.MethodName = attr?.MethodName;
            if (attr?.MethodName == null)
                inst.MethodName = NamingStrategy.GetRpcMethodName(method.Name, false);
            else
                inst.MethodName = NamingStrategy.GetRpcMethodName(attr.MethodName, true);
            if (scopeAttribute?.MethodPrefix != null)
                inst.MethodName = scopeAttribute.MethodPrefix + inst.MethodName;
            inst.IsNotification = attr?.IsNotification ?? false;
            inst.AllowExtensionData = attr?.AllowExtensionData ?? false;
            inst.ReturnParameter = CreateParameter(serviceType, method.ReturnParameter, attr, scopeAttribute);
            inst.Parameters = method.GetParameters()
                .Select(p => CreateParameter(serviceType, p, attr, scopeAttribute))
                .ToList();
            //inst.Cancellable = attr?.Cancellable
            //                   ?? inst.Parameters.Any(p => p.ParameterType == typeof(CancellationToken));
            inst.Invoker = new ReflectionJsonRpcMethodInvoker(serviceType, method);
            return inst;
        }

        protected virtual JsonRpcParameter CreateParameter(Type serviceType, ParameterInfo parameter,
            JsonRpcMethodAttribute methodAttribute, JsonRpcScopeAttribute scopeAttribute)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (parameter.IsOut || parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer)
                throw new NotSupportedException("Argument with out, ref, or of pointer type is not supported.");
            // parameter.Name == null for return parameter
            var taskResultType = Utility.GetTaskResultType(parameter.ParameterType);
            if (String.IsNullOrWhiteSpace(parameter.Name) != true && taskResultType != null)
                throw new NotSupportedException("Argument with type of System.Threading.Task is not supported.");
            var attr = parameter.GetCustomAttribute<JsonRpcParameterAttribute>();
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
    }
}
