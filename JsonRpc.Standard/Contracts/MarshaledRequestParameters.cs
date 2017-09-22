using System.Threading;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard.Contracts
{

    public struct MarshaledRequestParameters
    {
        public MarshaledRequestParameters(JToken parameters, CancellationToken cancellationToken)
        {
            Parameters = parameters;
            CancellationToken = cancellationToken;
        }

        public JToken Parameters { get; }

        public CancellationToken CancellationToken { get; }
    }

}
