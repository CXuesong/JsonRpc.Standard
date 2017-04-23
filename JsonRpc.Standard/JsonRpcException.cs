using System;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Indicates an general JSON RPC compatible exception.
    /// </summary>
    public class JsonRpcException : Exception
    {
        public JsonRpcException(string message) : this(JsonRpcErrorCode.InternalError, message)
        {
        }

        public JsonRpcException(JsonRpcErrorCode code, string message) : this((int) code, message, null)
        {
        }

        public JsonRpcException(int code, string message) : this(code, message, null)
        {
        }

        public JsonRpcException(int code, string message, object data) : this(code, message, data, null)
        {
        }

        public JsonRpcException(int code, string message, object data, Exception inner) : base(message, inner)
        {
            Code = code;
            RpcData = data == null ? null : JToken.FromObject(data, RpcSerializer.Serializer);
        }

        /// <summary>
        /// JSON RPC error code.
        /// </summary>
        public virtual int Code { get; }

        /// <summary>
        /// Additional information about the error. This property will be passed to client when
        /// returning JSON RPC error object.
        /// </summary>
        public virtual JToken RpcData { get; }


        public object GetRpcData(Type dataType)
        {
            return RpcData?.ToObject(dataType, RpcSerializer.Serializer);
        }

        public T GetRpcData<T>()
        {
            return RpcData == null ? default(T) : RpcData.ToObject<T>(RpcSerializer.Serializer);
        }
    }
}
