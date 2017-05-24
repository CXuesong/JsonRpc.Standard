using System;
using System.IO;
using System.Reflection;
using JsonRpc.Standard;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JsonRpc.AspNetCore
{
    /// <summary>
    /// Provides extension methods for JSON RPC on ASP.NET Core.
    /// </summary>
    public static class JsonRpcAspNetExtensions
    {
        /// <summary>
        /// Registers an <see cref="IJsonRpcServiceHost"/> singleton in <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <param name="builderConfigurator">The delegate used to configure the <see cref="JsonRpcServiceHostBuilder"/>.</param>
        /// <returns></returns>
        public static IServiceCollection AddJsonRpc(this IServiceCollection serviceCollection, Action<JsonRpcServiceHostBuilder> builderConfigurator)
        {
            if (serviceCollection == null) throw new ArgumentNullException(nameof(serviceCollection));
            serviceCollection.AddSingleton(provider =>
            {
                var builder = new JsonRpcServiceHostBuilder
                {
                    LoggerFactory = provider.GetService<ILoggerFactory>(),
                };
                builderConfigurator(builder);
                return builder.Build();
            });
            return serviceCollection;
        }

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
                    ResponseMessage response;
                    if (context.Request.Method != "POST")
                    {
                        response = new ResponseMessage(MessageId.Empty,
                            new ResponseError(JsonRpcErrorCode.InvalidRequest, "Only POST method is supported."));
                        goto WRITE_RESPONSE;
                    }
                    // {"method":""}        // 13 characters
                    if (context.Request.ContentLength < 12)
                    {
                        response = new ResponseMessage(MessageId.Empty,
                            new ResponseError(JsonRpcErrorCode.InvalidRequest, "The request body is too short."));
                        goto WRITE_RESPONSE;
                    }
                    var host = serviceHostFactory(context);
                    RequestMessage message;
                    try
                    {
                        using (var reader = new StreamReader(context.Request.Body))
                            message = (RequestMessage) Message.LoadJson(reader);
                    }
                    catch (JsonReaderException ex)
                    {
                        response = new ResponseMessage(MessageId.Empty,
                            new ResponseError(JsonRpcErrorCode.InvalidRequest, ex.Message));
                        goto WRITE_RESPONSE;
                    }
                    catch (Exception ex)
                    {
                        response = new ResponseMessage(MessageId.Empty, ResponseError.FromException(ex));
                        goto WRITE_RESPONSE;
                    }
                    context.RequestAborted.ThrowIfCancellationRequested();
                    var features = new AspNetCoreFeatureCollection(context);
                    var task = host.InvokeAsync(message, features, context.RequestAborted);
                    // For notification, we don't wait for the task.
                    response = message.IsNotification ? null : await task.ConfigureAwait(false);
                    WRITE_RESPONSE:
                    context.RequestAborted.ThrowIfCancellationRequested();
                    if (response == null) return;
                    context.Response.ContentType = "application/json";
                    var responseContent = response.ToString();
                    if (response.Error != null)
                    {
                        switch (response.Error.Code)
                        {
                            case (int) JsonRpcErrorCode.MethodNotFound:
                                context.Response.StatusCode = 404;
                                break;
                            case (int) JsonRpcErrorCode.InvalidRequest:
                                context.Response.StatusCode = 400;
                                break;
                            default:
                                context.Response.StatusCode = 500;
                                break;
                        }
                    }
                    using (var writer = new StreamWriter(context.Response.Body))
                    {
                        await writer.WriteAsync(responseContent);
                    }
                    return;
                }
                await next().ConfigureAwait(false);
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

        /// <summary>
        /// Gets the <see cref="HttpContext"/> containning the JSON RPC request.
        /// </summary>
        /// <param name="requestContext">The <see cref="RequestContext"/> instance.</param>
        public static HttpContext GetHttpContext(this RequestContext requestContext)
        {
            if (requestContext == null) throw new ArgumentNullException(nameof(requestContext));
            return requestContext.Features.Get<IAspNetCoreFeature>().HttpContext;
        }
    }
}
