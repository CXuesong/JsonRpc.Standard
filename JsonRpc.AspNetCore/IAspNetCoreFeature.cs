using System;
using System.Collections.Generic;
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
        /// Gets the <see cref="HttpContext"/> containning the JSON RPC request.
        /// </summary>
        HttpContext HttpContext { get; }
    }

    internal class AspNetCoreFeature : IAspNetCoreFeature
    {
        public AspNetCoreFeature(HttpContext httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            HttpContext = httpContext;
        }

        /// <inheritdoc />
        public HttpContext HttpContext { get; }
    }
}
