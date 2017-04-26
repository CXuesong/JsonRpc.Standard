using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JsonRpc.Standard.Contracts;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// A builder for <see cref="IJsonRpcServiceHost"/>.
    /// </summary>
    public sealed class ServiceHostBuilder
    {
        private readonly List<Type> serviceTypes = new List<Type>();

        public ServiceHostBuilder()
        {

        }

        /// <summary>
        /// User-defined session object.
        /// </summary>
        public ISession Session { get; set; }

        /// <summary>
        /// Service host options.
        /// </summary>
        public JsonRpcServiceHostOptions Options { get; set; }

        /// <summary>
        /// The factory that creates the JSON RPC service instances to handle the requests.
        /// </summary>
        public IServiceFactory ServiceFactory { get; set; }

        /// <summary>
        /// Contract resolver that maps the JSON RPC methods to CLR service methods.
        /// </summary>
        public IJsonRpcContractResolver ContractResolver { get; set; }

        /// <summary>
        /// The binder that chooses the best match among a set of RPC methods.
        /// </summary>
        public IRpcMethodBinder MethodBinder { get; set; }

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
            serviceTypes.Add(serviceType);
        }

        /// <summary>
        /// Registers all the exposed JSON PRC methods in the specified service object type.
        /// </summary>
        /// <typeparam name="TService">A subtype of <see cref="JsonRpcService"/>.</typeparam>
        public void Register<TService>() where TService : IJsonRpcService
        {
            Register(typeof(TService));
        }

        /// <summary>
        /// Builds a instance that implements <see cref="IJsonRpcServiceHost"/>.
        /// </summary>
        public IJsonRpcServiceHost Build()
        {
            var cr = ContractResolver ?? JsonRpcContractResolver.Default;
            var contract = cr.CreateServerContract(serviceTypes);
            return new JsonRpcServiceHost(contract, Options)
            {
                ServiceFactory = ServiceFactory ?? DefaultServiceFactory.Default,
                Session = Session,
                MethodBinder = MethodBinder
            };
        }
    }
}
