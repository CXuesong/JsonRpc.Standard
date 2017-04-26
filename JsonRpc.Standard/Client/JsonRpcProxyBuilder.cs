using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using JsonRpc.Standard.Contracts;
using Newtonsoft.Json;

namespace JsonRpc.Standard.Client
{
    /// <summary>
    /// A builder class that at runtime implements the server-side methods
    /// defined in the contract interfaces with JSON RPC requests or notifications.
    /// </summary>
    public class JsonRpcProxyBuilder
    {
        private static int assemblyCounter = 0;

        private int typeCounter = 0;

        // Proxy Type --> Implementation Type
        private readonly Dictionary<Type, ProxyBuilderEntry> proxyTypeDict = new Dictionary<Type, ProxyBuilderEntry>();

        internal const string DynamicAssemblyNamePrefix = "JsonRpc.Standard.Client._$ProxyImpl";

        private readonly Lazy<AssemblyBuilder> _AssemblyBuilder;

        private readonly Lazy<ModuleBuilder> _ModuleBuilder;
        private IJsonRpcContractResolver _ContractResolver = JsonRpcContractResolver.Default;

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

        public IJsonRpcContractResolver ContractResolver
        {
            get { return _ContractResolver; }
            set { _ContractResolver = value ?? JsonRpcContractResolver.Default; }
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
        /// <returns>The implemented proxy instance, which can be casted to <see cref="stubType"/> afterwards.</returns>
        public object CreateProxy(JsonRpcClient client, Type stubType)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (stubType == null) throw new ArgumentNullException(nameof(stubType));
            if (!stubType.GetTypeInfo().IsInterface)
                throw new ArgumentException($"{stubType} is not a Type of interface.", nameof(stubType));
            ProxyBuilderEntry entry;
            lock (proxyTypeDict)
            {
                if (!proxyTypeDict.TryGetValue(stubType, out entry))
                {
                    entry = ImplementProxy(stubType);
                    proxyTypeDict.Add(stubType, entry);
                }
            }
            return entry.CreateInstance(client);
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
            return (T) CreateProxy(client, typeof(T));
        }

        private static readonly ConstructorInfo JsonRpcProxyBase_ctor =
            typeof(JsonRpcProxyBase).GetTypeInfo().DeclaredConstructors.First();

        private static readonly MethodInfo JsonRpcProxyBase_SendAsync =
            typeof(JsonRpcProxyBase).GetRuntimeMethods().First(m => m.Name == "SendAsync");

        private static readonly MethodInfo JsonRpcProxyBase_Send =
            typeof(JsonRpcProxyBase).GetRuntimeMethods().First(m => m.Name == "Send");

        private static readonly ConstructorInfo NotSupportedException_ctor1 =
            typeof(NotSupportedException).GetTypeInfo().DeclaredConstructors.First(c => c.GetParameters().Length == 1);

        protected virtual ProxyBuilderEntry ImplementProxy(Type stubType)
        {
            var contract = ContractResolver.CreateClientContract(new[] {stubType});
            var builder = ModuleBuilder.DefineType(NextProxyTypeName(), TypeAttributes.Class | TypeAttributes.Sealed,
                typeof(JsonRpcProxyBase), new[] {stubType});
            {
                var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis,
                    new[] {typeof(JsonRpcClient), typeof(IList<JsonRpcMethod>)});
                var gen = ctor.GetILGenerator();
                gen.Emit(OpCodes.Ldarg_0); // this
                gen.Emit(OpCodes.Ldarg_1); // client
                gen.Emit(OpCodes.Ldarg_2); // methodTable
                gen.Emit(OpCodes.Call, JsonRpcProxyBase_ctor);
                gen.Emit(OpCodes.Ret);
            }
            int memberCounter = 0;          // Used to generate method names.
            var methodTable = new List<JsonRpcMethod>();
            var interfaceMembers = stubType.GetTypeInfo().DeclaredMembers;
            foreach (var member in interfaceMembers)
            {
                var implName = "$impl$." + memberCounter;
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
                        ImplementProxyMethod(method, rpcMethod, methodTable.Count, impl);
                        methodTable.Add(rpcMethod);
                    }
                    else
                    {
                        var gen = impl.GetILGenerator();
                        gen.Emit(OpCodes.Ldstr, method.Name + "is not a JSON RPC method.");
                        gen.Emit(OpCodes.Newobj, NotSupportedException_ctor1);
                        gen.Emit(OpCodes.Throw);
                    }
                }
                else if(member is PropertyInfo property)
                {
                    throw new InvalidOperationException($"Cannot implement property member in \"{stubType}\".");
                }
            }
            return new ProxyBuilderEntry(builder.CreateTypeInfo().AsType(), methodTable);
        }

        protected virtual void ImplementProxyMethod(MethodInfo method, JsonRpcMethod rpcMethod, int methodIndex, MethodBuilder builder)
        {
            var gen = builder.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);              // this
            gen.Emit(OpCodes.Ldc_I4, methodIndex);  // 1st param of send[Async]
            var args = method.GetParameters();
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
                    // ..., array, index, value --> ...
                    gen.Emit(OpCodes.Stelem_Ref);
                }
            }
            var TResult = rpcMethod.ReturnParameter.ParameterType == typeof(void)
                ? typeof(bool)
                : rpcMethod.ReturnParameter.ParameterType;
            if (rpcMethod.ReturnParameter.IsTask)
            {
                gen.Emit(OpCodes.Call, JsonRpcProxyBase_SendAsync.MakeGenericMethod(TResult));
            }
            else
            {
                gen.Emit(OpCodes.Call, JsonRpcProxyBase_Send.MakeGenericMethod(TResult));
                if (rpcMethod.ReturnParameter.ParameterType == typeof(void))
                    gen.Emit(OpCodes.Pop);
            }
            gen.Emit(OpCodes.Ret);
        }

        protected class ProxyBuilderEntry
        {
            public ProxyBuilderEntry(Type proxyType, IList<JsonRpcMethod> methodTable)
            {
                if (proxyType == null) throw new ArgumentNullException(nameof(proxyType));
                if (methodTable == null) throw new ArgumentNullException(nameof(methodTable));
                ProxyType = proxyType;
                MethodTable = methodTable;
            }
            
            public Type ProxyType { get; }

            public IList<JsonRpcMethod> MethodTable { get; }

            public object CreateInstance(JsonRpcClient client)
            {
                return Activator.CreateInstance(ProxyType, client, MethodTable);
            }
        }
    }
}
