using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Server;

namespace UnitTestProject1
{
    /// <summary>
    /// This handler directly connects server to the client.
    /// </summary>
    public class JsonRpcDirectHandler : JsonRpcClientHandler
    {

        public JsonRpcDirectHandler(IJsonRpcServiceHost serviceHost)
        {
            if (serviceHost == null) throw new ArgumentNullException(nameof(serviceHost));
            ServiceHost = serviceHost;
        }

        /// <summary>
        /// Gets the undelying <see cref="IJsonRpcServiceHost"/>.
        /// </summary>
        public IJsonRpcServiceHost ServiceHost { get; }

        /// <inheritdoc />
        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ServiceHost.InvokeAsync(request, null, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }
}
