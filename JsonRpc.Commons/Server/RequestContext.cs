using System;
using System.Threading;
using JsonRpc.Messages;

namespace JsonRpc.Server
{
    /// <summary>
    /// Provides the context per JSON RPC request.
    /// </summary>
    public class RequestContext
    {
        public RequestContext(IJsonRpcServiceHost serviceHost, IServiceFactory serviceFactory,
            IFeatureCollection features, RequestMessage request, CancellationToken cancellationToken)
        {
            if (serviceHost == null) throw new ArgumentNullException(nameof(serviceHost));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ServiceHost = serviceHost;
            ServiceFactory = serviceFactory;
            Request = request;
            Features = features;
            if (!request.IsNotification)
                Response = new ResponseMessage(request.Id);
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the service host that initiates this request.
        /// </summary>
        public IJsonRpcServiceHost ServiceHost { get; }

        /// <summary>
        /// The factory that creates the JSON RPC service instances to handle the requests.
        /// </summary>
        public IServiceFactory ServiceFactory { get; }

        /// <summary>
        /// The features provided with the request.
        /// </summary>
        public IFeatureCollection Features { get; }

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
