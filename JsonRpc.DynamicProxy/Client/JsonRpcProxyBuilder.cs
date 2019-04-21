using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using JsonRpc.Client;
using JsonRpc.Contracts;

namespace JsonRpc.DynamicProxy.Client
{
    /// <summary>
    /// A builder class that at runtime implements the server-side methods
    /// defined in the contract interfaces with JSON RPC requests or notifications.
    /// </summary>
    /// <remarks>This class is thread-safe.</remarks>
    public class JsonRpcProxyBuilder
    {
        private static int assemblyCounter = 0;
        private int typeCounter = 0;

        // Proxy Type --> Implementation Type
        private readonly ConcurrentDictionary<Type, ProxyBuilderEntry> proxyTypeDict = new ConcurrentDictionary<Type, ProxyBuilderEntry>();

        internal const string DynamicAssemblyNamePrefix = "JsonRpc.DynamicProxy.Client.$_ProxyImpl";

        private readonly Lazy<AssemblyBuilder> _AssemblyBuilder;
        private readonly Lazy<ModuleBuilder> _ModuleBuilder;

        private static readonly JsonRpcContractResolver defaultContractResolver = new JsonRpcContractResolver();
        private static readonly NamedRequestMarshaler defaultRequestMarshaler = new NamedRequestMarshaler();

        private IJsonRpcContractResolver _ContractResolver = defaultContractResolver;
        private IJsonRpcRequestMarshaler _RequestMarshaler = defaultRequestMarshaler;

        public JsonRpcProxyBuilder()
        {
            var ct = Interlocked.Increment(ref assemblyCounter);
            ImplementedProxyAssemblyName = DynamicAssemblyNamePrefix + ct;
            ImplementedProxyNamespace = ImplementedProxyAssemblyName;
            _AssemblyBuilder = new Lazy<AssemblyBuilder>(() =>
            {
                var builder = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName(ImplementedProxyAssemblyName),
                    AssemblyBuilderAccess.RunAndCollect);
#if DEBUG
            builder.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(DebuggableAttribute).GetTypeInfo()
                    .DeclaredConstructors.First(c => c.GetParameters().Length == 1),
                new object[]
                {
                    DebuggableAttribute.DebuggingModes.Default |
                    DebuggableAttribute.DebuggingModes.DisableOptimizations
                }));
#endif
                return builder;
            });
            _ModuleBuilder = new Lazy<ModuleBuilder>(
                () => AssemblyBuilder.DefineDynamicModule(ImplementedProxyNamespace + ".tmp"));
        }

        /// <summary>
        /// Contract resolver that maps the JSON RPC methods to CLR service methods.
        /// </summary>
        public IJsonRpcContractResolver ContractResolver
        {
            get { return _ContractResolver; }
            set
            {
                value = value ?? defaultContractResolver;
                if (_ContractResolver != value)
                {
                    Volatile.Write(ref _ContractResolver, value);
                    proxyTypeDict.Clear();
                }
            }
        }

        /// <summary>
        /// The request marshaler used to convert the CLR parameter values into JSON RPC ones.
        /// </summary>
        public IJsonRpcRequestMarshaler RequestMarshaler
        {
            get { return _RequestMarshaler; }
            set { _RequestMarshaler = value ?? defaultRequestMarshaler; }
        }

        protected string ImplementedProxyNamespace { get; }

        protected string ImplementedProxyAssemblyName { get; }

        protected AssemblyBuilder AssemblyBuilder => _AssemblyBuilder.Value;

        protected ModuleBuilder ModuleBuilder => _ModuleBuilder.Value;

        protected string NextProxyTypeName()
        {
            var ct = Interlocked.Increment(ref typeCounter);
            return ImplementedProxyNamespace + "._$Impl" + ct;
        }

        /// <summary>
        /// Creates a proxy instance that implements the given stub type with JSON RPC.
        /// </summary>
        /// <param name="client">The JSON RPC client used to emit RPC requests.</param>
        /// <param name="stubType">The stub type (or contract) to be implemented. Should be an interface type.</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="stubType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="stubType"/> is not a Type of interface.</exception>
        /// <returns>The implemented proxy instance, which can be cast to <paramref name="stubType"/> afterwards.</returns>
        public object CreateProxy(JsonRpcClient client, Type stubType)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (stubType == null) throw new ArgumentNullException(nameof(stubType));
            var entry = proxyTypeDict.GetOrAdd(stubType, ImplementProxy);
            return entry.CreateInstance(client, RequestMarshaler);
        }

        /// <summary>
        /// Creates a proxy instance that implements the given stub type with JSON RPC.
        /// </summary>
        /// <typeparam name="T">The stub type (or contract) to be implemented. Should be an interface type.</typeparam>
        /// <param name="client">The JSON RPC client used to emit RPC requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><typeparamref name="T"/> is not a type of interface.</exception>
        /// <returns>The implemented proxy instance.</returns>
        public T CreateProxy<T>(JsonRpcClient client)
        {
            return (T)CreateProxy(client, typeof(T));
        }

        private static readonly MethodInfo JsonRpcRealProxy_SendAsync =
            typeof(JsonRpcRealProxy).GetRuntimeMethods().First(m => m.Name == "SendAsync");

        private static readonly MethodInfo JsonRpcRealProxy_Send =
            typeof(JsonRpcRealProxy).GetRuntimeMethods().First(m => m.Name == "Send" && m.IsGenericMethod);

        private static readonly MethodInfo JsonRpcRealProxy_SendNotification =
            typeof(JsonRpcRealProxy).GetRuntimeMethods().First(m => m.Name == "Send" && !m.IsGenericMethod);

        private static readonly ConstructorInfo NotImplementedException_ctor1 =
            typeof(NotImplementedException).GetTypeInfo().DeclaredConstructors.First(c => c.GetParameters().Length == 1);

        private static readonly Type[] emptyTypes = { };

        protected virtual ProxyBuilderEntry ImplementProxy(Type stubType)
        {
            if (stubType.GetTypeInfo().IsSealed)
                throw new ArgumentException("Cannot implement transparent proxy on a sealed type.", nameof(stubType));
            var contract = ContractResolver.CreateClientContract(new[] { stubType });
            var builder = ModuleBuilder.DefineType(NextProxyTypeName(),
                TypeAttributes.Class | TypeAttributes.Sealed,
                stubType.GetTypeInfo().IsInterface ? typeof(object) : stubType,
                stubType.GetTypeInfo().IsInterface ? new[] { stubType } : emptyTypes);
            var realProxy = builder.DefineField("$_RealProxy", typeof(JsonRpcRealProxy),
                FieldAttributes.Private | FieldAttributes.InitOnly);
            {
                var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis,
                    new[] { typeof(JsonRpcRealProxy) });
                var gen = ctor.GetILGenerator();
                gen.Emit(OpCodes.Ldarg_0); // this
                gen.Emit(OpCodes.Ldarg_1); // realproxy
                gen.Emit(OpCodes.Stfld, realProxy);
                gen.Emit(OpCodes.Ret);
            }
            int memberCounter = 0;          // Used to generate method names.
            var methodTable = new List<JsonRpcMethod>();
            foreach (var member in stubType.GetRuntimeMethods().Where(m => m.IsAbstract))
            {
                var implName = "$_Impl_" + memberCounter + member.Name;
                if (member is MethodInfo method)
                {
                    memberCounter++;
                    var impl = builder.DefineMethod(implName,
                        MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.Virtual |
                        MethodAttributes.NewSlot | MethodAttributes.HideBySig,
                        method.ReturnType,
                        method.GetParameters().Select(pa => pa.ParameterType).ToArray());
                    builder.DefineMethodOverride(impl, method);
                    if (contract.Methods.TryGetValue(method, out var rpcMethod))
                    {
                        ImplementProxyMethod(method, rpcMethod, methodTable.Count, impl, realProxy);
                        methodTable.Add(rpcMethod);
                    }
                    else
                    {
                        var gen = impl.GetILGenerator();
                        gen.Emit(OpCodes.Ldstr, $"\"{method.Name}\" is not implemented by JSON RPC proxy builder.");
                        gen.Emit(OpCodes.Newobj, NotImplementedException_ctor1);
                        gen.Emit(OpCodes.Throw);
                    }
                }
            }
            return new ProxyBuilderEntry(builder.CreateTypeInfo().AsType(), methodTable);
        }

        protected virtual void ImplementProxyMethod(MethodInfo baseMethod, JsonRpcMethod rpcMethod, int methodIndex,
            MethodBuilder implBuilder, FieldInfo realProxyField)
        {
            var gen = implBuilder.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);                  // this
            gen.Emit(OpCodes.Ldfld, realProxyField);    // this.realProxy
            gen.Emit(OpCodes.Ldc_I4, methodIndex);      // this.realProxy, methodIndex
            var args = baseMethod.GetParameters();
            if (args.Length == 0)
            {
                gen.Emit(OpCodes.Ldnull);
            }
            else
            {
                gen.Emit(OpCodes.Ldc_I4, args.Length);
                gen.Emit(OpCodes.Newarr, typeof(object));
                for (var i = 0; i < args.Length; i++)
                {
                    gen.Emit(OpCodes.Dup);          // array
                    gen.Emit(OpCodes.Ldc_I4, i);    // index
                    gen.Emit(OpCodes.Ldarg, i + 1); // value
                    if (args[i].ParameterType.GetTypeInfo().IsValueType)
                        gen.Emit(OpCodes.Box, args[i].ParameterType);
                    // this.realProxy, methodIndex, array, array, index, value
                    gen.Emit(OpCodes.Stelem_Ref);
                    // this.realProxy, methodIndex, array
                }
            }
            var TResult = rpcMethod.ReturnParameter.ParameterType;
            if (rpcMethod.ReturnParameter.IsTask)
            {
                gen.Emit(OpCodes.Call, JsonRpcRealProxy_SendAsync.MakeGenericMethod(
                    TResult == typeof(void) ? typeof(object) : TResult));
            }
            else
            {
                if (rpcMethod.IsNotification)
                {
                    // Notification. invoke and forget.
                    if (rpcMethod.ReturnParameter.ParameterType != typeof(void))
                        throw new InvalidOperationException("Notification method can only return void or Task.");
                    gen.Emit(OpCodes.Call, JsonRpcRealProxy_SendNotification);
                }
                else
                {
                    // Message. invoke and wait.
                    if (TResult == typeof(void))
                    {
                        gen.Emit(OpCodes.Call, JsonRpcRealProxy_Send.MakeGenericMethod(typeof(object)));
                        gen.Emit(OpCodes.Pop);
                    }
                    else
                    {
                        gen.Emit(OpCodes.Call, JsonRpcRealProxy_Send.MakeGenericMethod(TResult));
                    }
                }
            }
            gen.Emit(OpCodes.Ret);
        }

        protected class ProxyBuilderEntry
        {
            private JsonRpcRealProxy _LastRealProxy;

            public ProxyBuilderEntry(Type proxyType, IList<JsonRpcMethod> methodTable)
            {
                ProxyType = proxyType ?? throw new ArgumentNullException(nameof(proxyType));
                MethodTable = methodTable ?? throw new ArgumentNullException(nameof(methodTable));
            }

            public Type ProxyType { get; }

            public IList<JsonRpcMethod> MethodTable { get; }

            public object CreateInstance(JsonRpcClient client, IJsonRpcRequestMarshaler marshaler)
            {
                var realProxy = _LastRealProxy;
                if (realProxy == null || realProxy.Client != client
                    || realProxy.MethodTable != MethodTable || realProxy.Marshaler != marshaler)
                {
                    realProxy = new JsonRpcRealProxy(client, MethodTable, marshaler);
                    Volatile.Write(ref _LastRealProxy, realProxy);
                }
                return Activator.CreateInstance(ProxyType, realProxy);
            }
        }
    }
}
