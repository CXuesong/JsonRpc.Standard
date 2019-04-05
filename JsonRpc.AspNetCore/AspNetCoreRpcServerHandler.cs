using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Server;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace JsonRpc.AspNetCore
{
    
    /// <summary>
    /// A <see cref="JsonRpcServerHandler"/> that transfers the requests from either a middleware
    /// or in the MVC controller.
    /// </summary>
    public class AspNetCoreRpcServerHandler : JsonRpcServerHandler
    {

        private static readonly Encoding UTF8NoBom = new UTF8Encoding(false);

        private Encoding _Encoding = UTF8NoBom;

        /// <inheritdoc />
        public AspNetCoreRpcServerHandler(IJsonRpcServiceHost serviceHost) : base(serviceHost)
        {
        }
        
        /// <summary>
        /// Encoding of the emitted messages.
        /// </summary>
        public Encoding Encoding
        {
            get => _Encoding;
            set => _Encoding = value ?? UTF8NoBom;
        }

        /// <summary>
        /// Content-Type header value of the emitted messages.
        /// </summary>
        /// <value>Content-Type header value, or <c>null</c> to suppress Content-Type header.</value>
        public string ResponseContentType { get; set; } = "application/json-rpc";

        /// <summary>
        /// Whether to follow the <see cref="ResponseContentType"/> with a "charset=xxx" part
        /// when writing messages to the stream. This property has no effect if <see cref="ResponseContentType"/>
        /// is null.
        /// </summary>
        public bool EmitContentCharset { get; set; } = true;

        /// <summary>
        /// Processes the JSON-RPC request contained in the HTTP request body,
        /// and writes the response to the HTTP response body.
        /// </summary>
        /// <param name="context">The HTTP request context.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
        public async Task ProcessRequestAsync(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ResponseMessage response;
            // {"method":""}        // 13 characters
            if (context.Request.ContentLength < 12)
            {
                response = new ResponseMessage(MessageId.Empty,
                    new ResponseError(JsonRpcErrorCode.InvalidRequest, "The request body is too short."));
                goto WRITE_RESPONSE;
            }
            RequestMessage message;
            try
            {
                using (var reader = new StreamReader(context.Request.Body))
                    message = (RequestMessage)Message.LoadJson(reader);
            }
            catch (JsonReaderException ex)
            {
                response = new ResponseMessage(MessageId.Empty,
                    new ResponseError(JsonRpcErrorCode.InvalidRequest, ex.Message));
                goto WRITE_RESPONSE;
            }
            catch (Exception ex)
            {
                response = new ResponseMessage(MessageId.Empty, ResponseError.FromException(ex, false));
                goto WRITE_RESPONSE;
            }
            context.RequestAborted.ThrowIfCancellationRequested();
            response = await ProcessRequestAsync(message, context, false);
            WRITE_RESPONSE:
            if (response == null) return;
            if (ResponseContentType != null)
            {
                context.Response.ContentType = ResponseContentType;
                if (EmitContentCharset) ResponseContentType += ";charset=" + Encoding.WebName;
            }
            // For notification, we don't wait for the task.
            var responseContent = response.ToString();
            if (response.Error != null)
            {
                switch (response.Error.Code)
                {
                    case (int)JsonRpcErrorCode.MethodNotFound:
                        context.Response.StatusCode = 404;
                        break;
                    case (int)JsonRpcErrorCode.InvalidRequest:
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
        }

        /// <summary>
        /// Processes the specified JSON-RPC request with certain <see cref="HttpContext"/>, and returns the response.
        /// </summary>
        /// <param name="message">The message to be processed.</param>
        /// <param name="context">The HTTP request context.</param>
        /// <returns>The JSON-RPC response, or <c>null</c> if there's no such response.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="message"/> or <paramref name="context"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>This method will enable <see cref="IAspNetCoreFeature"/> in invoked JSON RPC service handler.</para>
        /// <para>This method will use <see cref="HttpContext.RequestAborted"/> as cancellation token passed into <see cref="RequestContext"/>.</para>
        /// <para>This overload do not wait for the JSON-RPC response if the request is a notification message.</para>
        /// </remarks>
        public Task<ResponseMessage> ProcessRequestAsync(RequestMessage message, HttpContext context)
        {
            return ProcessRequestAsync(message, context, false);
        }

        /// <summary>
        /// Processes the specified JSON-RPC request with certain <see cref="HttpContext"/>, and returns the response.
        /// </summary>
        /// <param name="message">The message to be processed.</param>
        /// <param name="context">The HTTP request context.</param>
        /// <param name="waitForNotification">Either to wait for the handler for the notification request to finish before completing the task.</param>
        /// <returns>The JSON-RPC response, or <c>null</c> if there's no such response.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="message"/> or <paramref name="context"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>This method will enable <see cref="IAspNetCoreFeature"/> in invoked JSON RPC service handler.</para>
        /// <para>This method will use <see cref="HttpContext.RequestAborted"/> as cancellation token passed into <see cref="RequestContext"/>.</para>
        /// </remarks>
        public async Task<ResponseMessage> ProcessRequestAsync(RequestMessage message, HttpContext context, bool waitForNotification)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var features = new AspNetCoreFeatureCollection(DefaultFeatures, context);
            var task = ServiceHost.InvokeAsync(message, features, context.RequestAborted);
            if (waitForNotification || !message.IsNotification) return await task.ConfigureAwait(false);
            return null;
        }
    }
}
