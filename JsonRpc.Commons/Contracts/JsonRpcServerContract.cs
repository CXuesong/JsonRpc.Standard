using System.Collections.Generic;

namespace JsonRpc.Contracts
{
    /// <summary>
    /// Contract details used in JSON RPC calls.
    /// </summary>
    public class JsonRpcServerContract
    {
        private IDictionary<string, IList<JsonRpcMethod>> _Methods;

        /// <summary>
        /// Gets a dictionary that maps JSON RPC method name to a list of candidate methods.
        /// </summary>
        public IDictionary<string, IList<JsonRpcMethod>> Methods
        {
            get
            {
                if (_Methods == null) _Methods = new Dictionary<string, IList<JsonRpcMethod>>();
                return _Methods;
            }
            set { _Methods = value; }
        }
    }
}
