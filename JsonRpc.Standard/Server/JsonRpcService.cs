using JsonRpc.Standard.Contracts;

namespace JsonRpc.Standard.Server
{
    public interface IJsonRpcService
    {
        /// <summary>
        /// Gets or sets the <see cref="RequestContext"/> of current request.
        /// </summary>
        RequestContext RequestContext { get; set; }
    }

    /// <summary>
    /// Base class for providing JSON RPC service.
    /// </summary>
    public class JsonRpcService : IJsonRpcService
    {
        /// <summary>
        /// Gets or sets the <see cref="RequestContext"/> of current request.
        /// </summary>
        public RequestContext RequestContext { get; set; }
    }
}
