using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace JsonRpc.Standard.Server
{
    public class RequestContext
    {
        public RequestContext(ServiceContext serviceContext, ISession session, GeneralRequestMessage request, CancellationToken cancellationToken)
        {
            if (serviceContext == null) throw new ArgumentNullException(nameof(serviceContext));
            if (request == null) throw new ArgumentNullException(nameof(request));
            Request = request;
            Session = session;
            CancellationToken = cancellationToken;
            ServiceContext = serviceContext;
        }
        public ServiceContext ServiceContext { get; }

        public GeneralRequestMessage Request { get; }

        public ISession Session { get; }

        public CancellationToken CancellationToken { get; }

    }
}
