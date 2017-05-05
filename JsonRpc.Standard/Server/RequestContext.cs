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
            RequestMessage request, CancellationToken cancellationToken)
        {
            if (serviceHost == null) throw new ArgumentNullException(nameof(serviceHost));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ServiceHost = serviceHost;
            Session = session;
            Request = request;
            if (!request.IsNotification)
                Response = new ResponseMessage(request.Id);
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the service host that initiates this request.
        /// </summary>
        public IJsonRpcServiceHost ServiceHost { get; }

        /// <summary>
        /// Gets the service host or user defined session object.
        /// </summary>
        public ISession Session { get; }

        /// <summary>
        /// Gets the request message.
        /// </summary>
        public RequestMessage Request { get; }

        /// <summary>
        /// The response message to be sent. If this property is not <c>null</c>,
        /// it will take precedence to the return value of the invoked CLR method.
        /// </summary>
        public ResponseMessage Response { get; }

        /// <summary>
        /// The <see cref="CancellationToken"/> used to cancel this request. This token may be
        /// checked in the RPC method.
        /// </summary>
        public CancellationToken CancellationToken { get; }

    }
}
