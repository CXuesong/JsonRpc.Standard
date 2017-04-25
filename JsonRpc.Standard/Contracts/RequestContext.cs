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
        public RequestContext(ISession session, GeneralRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Session = session;
            Request = request;
            CancellationToken = cancellationToken;
        }

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
