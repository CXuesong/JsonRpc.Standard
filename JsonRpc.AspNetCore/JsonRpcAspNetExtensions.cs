using System;
using System.IO;
using System.Reflection;
using JsonRpc.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JsonRpc.AspNetCore
{
    /// <summary>
    /// Provides extension methods for JSON RPC on ASP.NET Core.
    /// </summary>
    public static class JsonRpcAspNetExtensions
    {
        /// <summary>
        /// Registers <see cref="IJsonRpcServiceHost"/> and <see cref="AspNetCoreRpcServerHandler"/> singletons
        /// in <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <param name="setupAction">The delegate used to configure the <see cref="JsonRpcServiceHostBuilder"/>.</param>
        /// <returns>A builder used to register JSON RPC services and middlewares.</returns>
        public static IJsonRpcBuilder AddJsonRpc(this IServiceCollection serviceCollection,
            Action<JsonRpcOptions> setupAction)
        {
            if (serviceCollection == null) throw new ArgumentNullException(nameof(serviceCollection));
            var options = new JsonRpcOptions();
            setupAction?.Invoke(options);
            var builder = new JsonRpcBuilder(options, serviceCollection);
            serviceCollection.AddSingleton(provider => builder.BuildServiceHost(provider));
            serviceCollection.AddSingleton<AspNetCoreRpcServerHandler>();
            return builder;
        }

        /// <summary>
        /// Uses <see cref="AspNetCoreRpcServerHandler"/> to handle the JSON RPC requests on certain URL.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="requestPath">The request path that should be treated as JSON RPC call.</param>
        /// <param name="serverHandlerFactory">The factory that builds server handler to handle the requests.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder builder, string requestPath,
            Func<HttpContext, AspNetCoreRpcServerHandler> serverHandlerFactory)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (requestPath == null) throw new ArgumentNullException(nameof(requestPath));
            if (serverHandlerFactory == null) throw new ArgumentNullException(nameof(serverHandlerFactory));
            builder.Use(async (context, next) =>
            {
                if (context.Request.Path.Value == requestPath)
                {
                    if (context.Request.Method != "POST")
                    {
                        context.Response.StatusCode = 405;
                        context.Response.ContentType = "text/plain;charset=utf-8";
                        await context.Response.WriteAsync(
                            "Only HTTP POST method is supported.\r\n\r\n----------\r\nServed from CXuesong.JsonRpc.AspNetCore");
                        return;
                    }
                    await serverHandlerFactory(context).ProcessRequestAsync(context);
                    return;
                }
                await next().ConfigureAwait(false);
            });
            return builder;
        }

        /// <summary>
        /// Uses <see cref="AspNetCoreRpcServerHandler"/> to handle the JSON RPC requests on certain URL.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="requestPath">The request path that should be treated as JSON RPC call.</param>
        /// <param name="serverHandler">The server handler to handle the requests.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder builder, string requestPath,
            AspNetCoreRpcServerHandler serverHandler)
        {
            if (serverHandler == null) throw new ArgumentNullException(nameof(serverHandler));
            return UseJsonRpc(builder, requestPath, _ => serverHandler);
        }

        /// <summary>
        /// Uses <see cref="AspNetCoreRpcServerHandler"/> to handle the JSON RPC requests on certain URL.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="requestPath">The request path that should be treated as JSON RPC call.</param>
        /// <returns>The application builder.</returns>
        /// <remarks>This overload uses dependency injection to find the <see cref="AspNetCoreRpcServerHandler"/> instance.</remarks>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder builder, string requestPath)
        {
            return UseJsonRpc(builder, requestPath, _ => _.RequestServices.GetService<AspNetCoreRpcServerHandler>());
        }

        /// <summary>
        /// Gets the <see cref="HttpContext"/> containing the JSON RPC request.
        /// </summary>
        /// <param name="requestContext">The <see cref="RequestContext"/> instance.</param>
        /// <returns>The HttpContext provided by the JSON RPC request context, or <c>null</c> if no such context is available.</returns>
        public static HttpContext GetHttpContext(this RequestContext requestContext)
        {
            if (requestContext == null) throw new ArgumentNullException(nameof(requestContext));
            return requestContext.Features.Get<IAspNetCoreFeature>()?.HttpContext;
        }
    }
}
