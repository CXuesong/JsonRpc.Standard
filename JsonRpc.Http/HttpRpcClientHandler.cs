using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using JsonRpc.Messages;

namespace JsonRpc.Http
{
    /// <summary>
    /// Transmits JSON RPC calls in HTTP requests.
    /// </summary>
    public class HttpRpcClientHandler : JsonRpcClientHandler, IDisposable
    {
        private HttpClient myClient;
        private readonly bool disposeClient;
        private Encoding _Encoding;

        public HttpRpcClientHandler() : this((HttpClient) null)
        {

        }

        public HttpRpcClientHandler(HttpClient httpClient)
        {
            myClient = httpClient ?? new HttpClient();
            disposeClient = httpClient == null;
        }

        public HttpRpcClientHandler(HttpMessageHandler handler)
        {
            myClient = new HttpClient(handler);
            disposeClient = false;
        }

        /// <summary>
        /// HTTP method used when sending the client request.
        /// </summary>
        public HttpMethod HttpMethod { get; set; } = HttpMethod.Post;

        /// <summary>
        /// Encoding of the emitted messages.
        /// </summary>
        /// <remarks>Defaults to <see cref="Encoding.UTF8"/>.</remarks>
        public Encoding Encoding
        {
            get => _Encoding;
            set => _Encoding = value ?? Encoding.UTF8;
        }

        /// <summary>
        /// Endpoint URL of the JSON RPC web API.
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// Client User-Agent. The UA of JsonRpc.Http will be append to this value when sending the requests.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Content-Type header value of the emitted messages.
        /// </summary>
        /// <value>Content-Type header value, or <c>null</c> to suppress Content-Type header.</value>
        public string ContentType { get; set; } = "application/json-rpc";

        /// <summary>
        /// Authentication token header value.
        /// </summary>
        /// <value>Bearer Authentication token header value.</value>
        public string AuthToken { get; set; }

        // Looking for EmitContentCharset? No, StringContent will automatically append charset to the
        // end of the Content-Type of the requests.

        /// <inheritdoc />
        public override async Task<ResponseMessage> SendAsync(RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (myClient == null) throw new ObjectDisposedException(nameof(HttpRpcClientHandler));
            cancellationToken.ThrowIfCancellationRequested();
            OnMessageSending(request);
            var httpReq = GetHttpRequestMessage(request);
            cancellationToken.ThrowIfCancellationRequested();
            var httpResp = await myClient.SendAsync(httpReq, cancellationToken);
            var resp = await ParseHttpResponseMessageAsync(httpResp, cancellationToken);
            return resp;
        }

        /// <summary>
        /// Converts <see cref="RequestMessage"/> to <see cref="HttpRequestMessage"/>.
        /// </summary>
        protected virtual HttpRequestMessage GetHttpRequestMessage(RequestMessage request)
        {
            var req = new HttpRequestMessage(HttpMethod, EndpointUrl)
            {
                Content = new StringContent(request.ToString(), Encoding)
            };
            if (!string.IsNullOrEmpty(UserAgent)) req.Headers.UserAgent.ParseAdd(UserAgent);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("JsonRpc.Http", "0.3"));
            if (!string.IsNullOrEmpty(AuthToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            }

            return req;
        }

        /// <summary>
        /// Converts <see cref="HttpResponseMessage"/> to <see cref="ResponseMessage"/>.
        /// </summary>
        protected virtual async Task<ResponseMessage> ParseHttpResponseMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(result)) return null;
            var resp = Message.LoadJson(result);
            return (ResponseMessage) resp;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (myClient == null) return;
            if (disposeClient) myClient.Dispose();
            myClient = null;
        }
    }
}
