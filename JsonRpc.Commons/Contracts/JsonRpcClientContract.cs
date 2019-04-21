using System.Collections.Generic;
using System.Reflection;

namespace JsonRpc.Contracts
{
    /// <summary>
    /// Contract details used in JSON RPC calls.
    /// </summary>
    public class JsonRpcClientContract
    {
        private IDictionary<MethodInfo, JsonRpcMethod> _Methods;

        /// <summary>
        /// Gets a dictionary that maps JSON RPC method name to a list of candidate methods.
        /// </summary>
        public IDictionary<MethodInfo, JsonRpcMethod> Methods
        {
            get
            {
                if (_Methods == null) _Methods = new Dictionary<MethodInfo, JsonRpcMethod>();
                return _Methods;
            }
            set { _Methods = value; }
        }
    }
}
