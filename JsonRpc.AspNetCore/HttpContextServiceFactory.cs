using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JsonRpc.Standard.Server;
using Microsoft.AspNetCore.Http;

namespace JsonRpc.AspNetCore
{
    /// <summary>
    /// Instantiates service from the <see cref="IServiceProvider"/> found in the <see cref="HttpContext"/>
    /// of the provided requests.
    /// </summary>
    public class HttpContextServiceFactory : IServiceFactory
    {

        /// <summary>
        /// The default instance of <see cref="HttpContextServiceFactory"/>.
        /// </summary>
        public static readonly HttpContextServiceFactory Default = new HttpContextServiceFactory();

        /// <inheritdoc />
        public IJsonRpcService CreateService(Type serviceType, RequestContext context)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (!typeof(IJsonRpcService).GetTypeInfo().IsAssignableFrom(serviceType.GetTypeInfo()))
                throw new ArgumentException("serviceType is not a derived type of IJsonRpcService.", nameof(serviceType));
            var httpContext = context.GetHttpContext();
            if (httpContext == null)
                throw new ArgumentException("The provided RequestContext does not have HttpContext information.",
                    nameof(context));
            return (IJsonRpcService) httpContext.RequestServices.GetService(serviceType);
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
