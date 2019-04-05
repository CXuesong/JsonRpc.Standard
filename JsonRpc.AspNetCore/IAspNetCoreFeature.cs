using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace JsonRpc.AspNetCore
{
    /// <summary>
    /// Provides information on ASP.NET Core context on the JSON RPC requests.
    /// </summary>
    public interface IAspNetCoreFeature
    {
        /// <summary>
        /// Gets the <see cref="HttpContext"/> containing the JSON RPC request.
        /// </summary>
        HttpContext HttpContext { get; }
    }

    /// <summary>
    /// The default implementation of <see cref="IAspNetCoreFeature"/>,
    /// which is simply a wrapper of <see cref="HttpContext"/>.
    /// </summary>
    public class AspNetCoreFeature : IAspNetCoreFeature
    {

        private static readonly object jsonRpcAspNetCoreFeatureWrapperKey = new { Name = "JsonRpcAspNetCoreFeatureWrapperKey" };

        private AspNetCoreFeature(HttpContext httpContext)
        {
            Debug.Assert(httpContext != null);
            HttpContext = httpContext;
        }

        /// <summary>
        /// Gets an <see cref="AspNetCoreFeature"/> wrapper from the specified <see cref="HttpContext"/> instance.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is <c>null</c>.</exception>
        public static AspNetCoreFeature FromHttpContext(HttpContext httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            if (httpContext.Items.TryGetValue(jsonRpcAspNetCoreFeatureWrapperKey, out var feature) || feature == null)
            {
                feature = new AspNetCoreFeature(httpContext);
                httpContext.Items[jsonRpcAspNetCoreFeatureWrapperKey] = feature;
            }
            return (AspNetCoreFeature) feature;
        }

        /// <inheritdoc />
        public HttpContext HttpContext { get; }
    }
}
