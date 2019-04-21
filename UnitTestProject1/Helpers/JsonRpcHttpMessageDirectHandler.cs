using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;
using JsonRpc.Server;

namespace UnitTestProject1.Helpers
{
    public class JsonRpcHttpMessageDirectHandler : HttpMessageHandler
    {

        public JsonRpcHttpMessageDirectHandler(IJsonRpcServiceHost serviceHost)
        {
            if (serviceHost == null) throw new ArgumentNullException(nameof(serviceHost));
            ServiceHost = serviceHost;
        }

        public IJsonRpcServiceHost ServiceHost { get; }
        
        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var req = (RequestMessage) Message.LoadJson(await request.Content.ReadAsStringAsync());
            var resp = await ServiceHost.InvokeAsync(req, null, cancellationToken);
            var httpResp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(resp.ToString(), Encoding.UTF8, "application/json")
            };
            return httpResp;
        }
    }
}
