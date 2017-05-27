using System;
using System.Collections.Generic;
using System.Text;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JsonRpc.AspNetCore
{
    /// <summary>
    /// Options for JSON RPC server.
    /// </summary>
    public class JsonRpcOptions
    {

        /// <summary>
        /// The factory that creates the JSON RPC service instances to handle the requests.
        /// </summary>
        /// <remarks>
        /// The default value uses <see cref="HttpContextServiceFactory.Default"/>.
        /// Set this property to <c>null</c> is equavalent to setting it to <see cref="DefaultServiceFactory.Default"/>.
        /// </remarks>
        public IServiceFactory ServiceFactory { get; set; } = HttpContextServiceFactory.Default;

        /// <summary>
        /// Contract resolver that maps the JSON RPC methods to CLR service methods.
        /// </summary>
        public IJsonRpcContractResolver ContractResolver { get; set; }

        /// <summary>
        /// The binder that chooses the best match among a set of RPC methods.
        /// </summary>
        public IJsonRpcMethodBinder MethodBinder { get; set; }

        /// <summary>
        /// The logger factory used to get a logger for the service host.
        /// </summary>
        public ILoggerFactory LoggerFactory { get; set; }

        /// <summary>
        /// Whether to automatically register all the <see cref="IJsonRpcService"/> types
        /// used in the service host in the current <see cref="IServiceCollection"/>
        /// with transcient lifetime.
        /// </summary>
        /// <remarks>
        /// If you plan to work with your own <see cref="ServiceFactory"/>, you may
        /// need to set this property to <c>false</c>.
        /// </remarks>
        public bool InjectServiceTypes { get; set; } = true;
    }

    /// <summary>
    /// Extension methods for <see cref="JsonRpcOptions"/>.
    /// </summary>
    public static class JsonRpcOptionsExtensions
    {
        /// <summary>
        /// Uses a <see cref="JsonRpcContractResolver"/> with <see cref="CamelCaseJsonRpcNamingStrategy"/> and
        /// <see cref="CamelCaseJsonValueConverter"/>.
        /// </summary>
        public static JsonRpcOptions UseCamelCaseContractResolver(this JsonRpcOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.ContractResolver = new JsonRpcContractResolver
            {
                NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
                ParameterValueConverter = new CamelCaseJsonValueConverter(),
            };
            return options;
        }
    }
}
