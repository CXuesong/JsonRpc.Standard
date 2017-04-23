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

namespace JsonRpc.Standard.Client
{
    public class JsonRpcProxyBuilder
    {
        private static int assemblyCounter = 0;

        private int typeCounter = 0;

        // Proxy Type --> Implementation Type
        private readonly Dictionary<Type, Type> proxyTypeDict = new Dictionary<Type, Type>();

        private readonly Lazy<AssemblyBuilder> _AssemblyBuilder;

        private readonly Lazy<ModuleBuilder> _ModuleBuilder;

        protected readonly string implementedProxyNamespace;

        public JsonRpcProxyBuilder()
        {
            var ct = Interlocked.Increment(ref assemblyCounter);
            implementedProxyNamespace = "JsonRpc.Standard.Client.ProxyImpl_" + ct;
            _AssemblyBuilder = new Lazy<AssemblyBuilder>(() =>
            {
                var builder = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName(implementedProxyNamespace),
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
            _ModuleBuilder = new Lazy<ModuleBuilder>(() => AssemblyBuilder.DefineDynamicModule("ProxyImpl.Main.dll"));
        }

        protected AssemblyBuilder AssemblyBuilder => _AssemblyBuilder.Value;

        protected ModuleBuilder ModuleBuilder => _ModuleBuilder.Value;

        protected string NextProxyTypeName()
        {
            var ct = Interlocked.Increment(ref typeCounter);
            return implementedProxyNamespace + "._Impl" + ct;
        }

        public object CreateProxy(JsonRpcClient client, Type proxyType)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (!proxyType.GetTypeInfo().IsInterface)
                throw new ArgumentException($"{proxyType} is not a interface type.", nameof(proxyType));
            Type implType;
            lock (proxyTypeDict)
            {
                if (!proxyTypeDict.TryGetValue(proxyType, out implType))
                {
                    implType = ImplementProxy(proxyType);
                    proxyTypeDict.Add(proxyType, implType);
                }
            }
            return Activator.CreateInstance(implType, client);
        }

        public T CreateProxy<T>(JsonRpcClient client)
        {
            return (T) CreateProxy(client, typeof(T));
        }

        private static readonly ConstructorInfo JsonRpcProxyBase_ctor =
            typeof(JsonRpcProxyBase).GetTypeInfo().DeclaredConstructors.First();

        private static readonly ConstructorInfo NotSupportedException_ctor1 =
            typeof(NotSupportedException).GetTypeInfo().DeclaredConstructors.First(c => c.GetParameters().Length == 1);

        protected virtual Type ImplementProxy(Type proxyType)
        {
            var builder = ModuleBuilder.DefineType(NextProxyTypeName(), TypeAttributes.Class | TypeAttributes.Sealed,
                typeof(JsonRpcProxyBase), new[] {proxyType});
            {
                var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Any,
                    new[] {typeof(JsonRpcClient)});
                var gen = ctor.GetILGenerator();
                gen.Emit(OpCodes.Ldarg_0); // this
                gen.Emit(OpCodes.Ldarg_1); // client
                gen.Emit(OpCodes.Call, JsonRpcProxyBase_ctor);
                gen.Emit(OpCodes.Ret);
            }
            int methodCounter = 0;
            foreach (var p in ResolveMethods(proxyType))
            {
                methodCounter++;
                var impl = builder.DefineMethod("$impl$." + methodCounter,
                    MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.Virtual |
                    MethodAttributes.NewSlot | MethodAttributes.HideBySig,
                    p.Key.ReturnType,
                    p.Key.GetParameters().Select(pa => pa.ParameterType).ToArray());
                if (p.Value == null)
                {
                    var gen = impl.GetILGenerator();
                    gen.Emit(OpCodes.Ldstr, p.Key + "is not a JSON RPC method.");
                    gen.Emit(OpCodes.Newobj, NotSupportedException_ctor1);
                    gen.Emit(OpCodes.Throw);
                }
                else
                {
                    ImplementProxyMethod(p.Key, p.Value, impl);
                }
            }
            return builder.AsType();
        }

        protected virtual void ImplementProxyMethod(MethodInfo method, JsonRpcMethod rpcMethod, MethodBuilder builder)
        {
            var args = new List<ParameterExpression> {Expression.Parameter(builder.DeclaringType, "this")};
            args.AddRange(builder.GetParameters().Select(a => Expression.Parameter(a.ParameterType, a.Name)));
            var argListExpr = rpcMethod.Parameters.Count > 0
                ? (Expression) Expression.NewArrayInit(typeof(object), args.Skip(1))
                : Expression.Constant(null);
            Expression sendExpr;
            if (rpcMethod.ReturnParameter.IsTask)
            {
                sendExpr = Expression.Call(args[0], "SendAsync",
                    new[] {rpcMethod.ReturnParameter.ParameterType},
                    argListExpr);
            }
            else
            {
                sendExpr = Expression.Call(args[0], "Send",
                    new[] { rpcMethod.ReturnParameter.ParameterType },
                    argListExpr);
            }
            var body = Expression.Block(args, sendExpr);
            var lambda = Expression.Lambda(body, args);
            
        }

        /// <summary>
        /// Resolves all the RPC methods from the specified service type.
        /// </summary>
        /// <param name="serviceType"></param>
        /// <returns></returns>
        protected virtual IEnumerable<KeyValuePair<MethodInfo, JsonRpcMethod>> ResolveMethods(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            foreach (var m in serviceType.GetRuntimeMethods())
            {
                if (m.GetCustomAttribute<JsonRpcMethodAttribute>() != null)
                {
                    var rpcm = JsonRpcMethod.FromMethod(serviceType, m, true);
                    yield return new KeyValuePair<MethodInfo, JsonRpcMethod>(m, rpcm);
                }
                else
                {
                    yield return new KeyValuePair<MethodInfo, JsonRpcMethod>(m, null);
                }
            }
        }
    }
}
