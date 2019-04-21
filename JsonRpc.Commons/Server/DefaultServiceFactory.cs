using System;
using System.Reflection;

namespace JsonRpc.Server
{
    /// <summary>
    /// A factory that creates the specified JSON RPC service instance.
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// Creates the specified JSON RPC service instance.
        /// </summary>
        /// <param name="serviceType">The desired service type.</param>
        /// <param name="context">The request context.</param>
        /// <returns>A service instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="serviceType"/> is not a derived type of <see cref="IJsonRpcService"/>.</exception>
        IJsonRpcService CreateService(Type serviceType, RequestContext context);

        /// <summary>
        /// Releases the specified service instance.
        /// </summary>
        /// <param name="service">The service instance to be released.</param>
        void ReleaseService(IJsonRpcService service);
    }

    /// <summary>
    /// Provides a default implementation of <see cref="IServiceFactory"/>.
    /// </summary>
    public class DefaultServiceFactory : IServiceFactory
    {
        internal static readonly DefaultServiceFactory Default = new DefaultServiceFactory();

        /// <inheritdoc />
        public IJsonRpcService CreateService(Type serviceType, RequestContext context)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (!typeof(IJsonRpcService).GetTypeInfo().IsAssignableFrom(serviceType.GetTypeInfo()))
                throw new ArgumentException("serviceType is not a derived type of IJsonRpcService.", nameof(serviceType));
            var service = (IJsonRpcService) Activator.CreateInstance(serviceType);
            return service;
        }

        /// <inheritdoc />
        public void ReleaseService(IJsonRpcService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            // Basic cleanup.
            service.RequestContext = null;
            var disposable = service as IDisposable;
            disposable?.Dispose();
        }
    }
}
