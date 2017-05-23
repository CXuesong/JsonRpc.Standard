using System;
using System.IO;
using JsonRpc.Standard;
using JsonRpc.Standard.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace JsonRpc.AspNetCore
{
    public static class JsonRpcExtensions
    {
        /// <summary>
        /// Uses <see cref="IJsonRpcServiceHost"/> to handle the JSON RPC requests on certain URL.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="requestPath">The request path that should be treated as JSON RPC call.</param>
        /// <param name="serviceHostFactory">The factory that builds service host to handle the requests.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder builder, string requestPath,
            Func<HttpContext, IJsonRpcServiceHost> serviceHostFactory)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (requestPath == null) throw new ArgumentNullException(nameof(requestPath));
            if (serviceHostFactory == null) throw new ArgumentNullException(nameof(serviceHostFactory));
            builder.Use(async (context, next) =>
            {
                if (context.Request.Path.Value == requestPath)
                {
                    var host = serviceHostFactory(context);
                    RequestMessage message;
                    ResponseMessage response;
                    try
                    {
                        using (var reader = new StreamReader(context.Request.Body))
                            message = (RequestMessage) Message.LoadJson(reader);
                    }
                    catch (Exception ex)
                    {
                        response = new ResponseMessage(MessageId.Empty, ResponseError.FromException(ex));
                        return;
                    }
                    context.RequestAborted.ThrowIfCancellationRequested();
                    var features = new AspNetCoreFeatureCollection(context);
                    response = await host.InvokeAsync(message, features, context.RequestAborted);
                    var responseContent = response.ToString();
                    context.Response.ContentType = "application/json-rpc";
                    using (var writer = new StreamWriter(context.Response.Body))
                    {
                        await writer.WriteAsync(responseContent);
                    }
                }
                await next();
            });
            return builder;
        }

        /// <summary>
        /// Uses <see cref="IJsonRpcServiceHost"/> to handle the JSON RPC requests on certain URL.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="requestPath">The request path that should be treated as JSON RPC call.</param>
        /// <param name="serviceHost">The service host to handle the requests.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder builder, string requestPath,
            IJsonRpcServiceHost serviceHost)

        {
            if (serviceHost == null) throw new ArgumentNullException(nameof(serviceHost));
            return UseJsonRpc(builder, requestPath, _ => serviceHost);
        }

        /// <summary>
        /// Uses <see cref="IJsonRpcServiceHost"/> to handle the JSON RPC requests on certain URL.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="requestPath">The request path that should be treated as JSON RPC call.</param>
        /// <returns>The application builder.</returns>
        /// <remarks>This overload uses dependency injection to find the <see cref="IJsonRpcServiceHost"/> instance.</remarks>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder builder, string requestPath)
        {
            return UseJsonRpc(builder, requestPath,
                _ => (IJsonRpcServiceHost) _.RequestServices.GetService(typeof(IJsonRpcServiceHost)));
        }
    }
}
