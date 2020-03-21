using System;
using System.Security;
using System.Text;
using JsonRpc.Messages;
using Newtonsoft.Json;
#if BCL_FEATURE_SERIALIZATION
using System.Runtime.Serialization;
#endif

namespace JsonRpc.Client
{
    /// <summary>
    /// The base exception class that indicates the general error of JSON RPC client.
    /// </summary>
#if BCL_FEATURE_SERIALIZATION
    [Serializable]
#endif
    public class JsonRpcClientException : Exception
    {
        public JsonRpcClientException(string message)
            : this(message, null)
        {
        }

        public JsonRpcClientException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if BCL_FEATURE_SERIALIZATION
        [SecuritySafeCritical]
        protected JsonRpcClientException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// The exception that indicates the violation of a server/client-side RPC contract.
    /// </summary>
#if BCL_FEATURE_SERIALIZATION
    [Serializable]
#endif
    public class JsonRpcContractException : JsonRpcClientException
    {
        public JsonRpcContractException(string message, Exception innerException)
            : this(message, null, innerException)
        {
        }

        public JsonRpcContractException(string message, Message rpcMessage)
            : this(message, rpcMessage, null)
        {
        }

        public JsonRpcContractException(string message, Message rpcMessage, Exception innerException)
            : base(message ?? "A server/client-side RPC contract has been violated.", innerException)
        {
            RpcMessage = rpcMessage;
        }

#if BCL_FEATURE_SERIALIZATION
        [SecuritySafeCritical]
        protected JsonRpcContractException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            RpcMessage = JsonConvert.DeserializeObject<Message>(info.GetString("RpcMessage"));
        }
#endif

        /// <summary>
        /// Gets the JSON RPC message that caused this exception.
        /// </summary>
        public Message RpcMessage { get; }

#if BCL_FEATURE_SERIALIZATION
        /// <inheritdoc />
        [SecurityCritical]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("RpcMessage", JsonConvert.SerializeObject(RpcMessage));
        }
#endif
    }

    /// <summary>
    /// The exception that indicates an error from remote RPC endpoint.
    /// </summary>
#if BCL_FEATURE_SERIALIZATION
    [Serializable]
#endif
    public class JsonRpcRemoteException : JsonRpcClientException
    {
        public JsonRpcRemoteException(string message, Exception innerException)
            : this(message, null, innerException)
        {
        }

        public JsonRpcRemoteException(string message, ResponseError error, Exception innerException)
            : base(message, innerException)
        {
            Error = error;

        }

        public JsonRpcRemoteException(ResponseError error)
            : this(error?.Message, error, null)
        {
            Initialize();
        }

#if BCL_FEATURE_SERIALIZATION
        [SecuritySafeCritical]
        protected JsonRpcRemoteException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            Error = JsonConvert.DeserializeObject<ResponseError>(info.GetString("Error"));
            Initialize();
        }
#endif

        private void Initialize()
        {
            if (Error?.Code == (int)JsonRpcErrorCode.UnhandledClrException)
            {
                RemoteException = Error.GetData<ClrExceptionErrorData>();
            }
        }

        /// <summary>
        /// The JSON RPC Error object that raises the exception.
        /// </summary>
        public ResponseError Error { get; }

        /// <summary>
        /// Remote CLR exception data, if available.
        /// </summary>
        public ClrExceptionErrorData RemoteException { get; private set; }

#if BCL_FEATURE_SERIALIZATION
        /// <inheritdoc />
        [SecurityCritical]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Error", JsonConvert.SerializeObject(Error));
        }
#endif

        /// <inheritdoc />
        public override string ToString()
        {
            if (RemoteException == null) return base.ToString();
            var sb = new StringBuilder(base.ToString());
            sb.AppendLine();
            sb.AppendLine("Remote Exception information:");
            RemoteException.ToString(sb, 0);
            return sb.ToString();
        }
    }
}
