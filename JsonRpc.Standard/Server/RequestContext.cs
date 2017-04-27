using System;
using System.Threading;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Provides the context per JSON RPC request.
    /// </summary>
    public class RequestContext
    {
        public RequestContext(IJsonRpcServiceHost serviceHost, ISession session,
            GeneralRequestMessage request, CancellationToken cancellationToken)
        {
            if (serviceHost == null) throw new ArgumentNullException(nameof(serviceHost));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ServiceHost = serviceHost;
            Session = session;
            Request = request;
            if (request is RequestMessage req)
                Response = new ResponseMessage(req.Id);
            CancellationToken = cancellationToken;
        }

        public IJsonRpcServiceHost ServiceHost { get; }

        public ISession Session { get; }

        /// <summary>
        /// The request message.
        /// </summary>
        public GeneralRequestMessage Request { get; }

        /// <summary>
        /// The response message to be sent.
        /// </summary>
        public ResponseMessage Response { get; }

        /// <summary>
        /// The <see cref="CancellationToken"/> used to cancel this request. This token may be
        /// checked in the RPC method.
        /// </summary>
        public CancellationToken CancellationToken { get; }

    }
}
