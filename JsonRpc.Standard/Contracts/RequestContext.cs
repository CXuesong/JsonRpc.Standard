using System;
using System.Threading;
using JsonRpc.Standard.Server;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Provides the context per JSON RPC request.
    /// </summary>
    public class RequestContext
    {
        public RequestContext(IJsonRpcServiceHost host, ISession session, GeneralRequestMessage request, CancellationToken cancellationToken)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (request == null) throw new ArgumentNullException(nameof(request));
            Host = host;
            Session = session;
            Request = request;
            CancellationToken = cancellationToken;
        }

        public IJsonRpcServiceHost Host { get; }

        public ISession Session { get; }

        /// <summary>
        /// The request message.
        /// </summary>
        public GeneralRequestMessage Request { get; }

        /// <summary>
        /// The <see cref="CancellationToken"/> used to cancel this request. This token may be
        /// checked in the RPC method.
        /// </summary>
        public CancellationToken CancellationToken { get; }

    }
}
