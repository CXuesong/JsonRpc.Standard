using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard
{
    /// <summary>
    /// An exception that indicates an general JSON RPC error.
    /// </summary>
    public class JsonRpcException : Exception
    {

        private static string BuildMessage(string message, ResponseError error)
        {
            if (message == null)
            {
                if (error != null)
                {
                    message = error.Message;
                    if (string.IsNullOrEmpty(message))
                        message = $"An JSON RPC error occured. Error code: {error.Code}.";
                }
                else
                {
                    message = "An JSON RPC error occured.";
                }
            }
            return message;
        }

        public JsonRpcException(string message) : this(message, null, null)
        {
        }

        public JsonRpcException(ResponseError error) : this(null, error)
        {
        }

        public JsonRpcException(string message, ResponseError error) : this(message, error, null)
        {
        }

        public JsonRpcException(string message, Exception innerException) : this(message, null, innerException)
        {
        }

        public JsonRpcException(string message, ResponseError error, Exception innerException) : base(
            BuildMessage(message, error), innerException)
        {
            Error = error;
        }

        /// <summary>
        /// JSON RPC error object.
        /// </summary>
        public ResponseError Error { get; }
    }
}
