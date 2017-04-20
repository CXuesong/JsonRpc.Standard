using System;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Indicates an error in JSON RPC parsing.
    /// </summary>
    public class JsonRpcException : Exception
    {
        public JsonRpcException(string message) : base(message)
        {

        }

        public JsonRpcException(string message, Exception inner) : base(message, inner)
        {
            
        }
    }
}
