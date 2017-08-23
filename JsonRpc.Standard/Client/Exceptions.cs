using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
#if NET45
using System.Runtime.Serialization;
#endif

namespace JsonRpc.Standard.Client
{
    /// <summary>
    /// The base exception class that indicates the general error of JSON RPC client.
    /// </summary>
#if NET45
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
        
#if NET45
        protected JsonRpcClientException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// The exception that indicates the violation of a server/client-side RPC contract.
    /// </summary>
#if NET45
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

#if NET45
        protected JsonRpcContractException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            RpcMessage = (Message)info.GetValue("RpcMessage", typeof(Message));
        }
#endif

        /// <summary>
        /// Gets the JSON RPC message that caused this exception.
        /// </summary>
        public Message RpcMessage { get; }

#if NET45
        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("RpcMessage", RpcMessage);
        }
#endif
    }

    /// <summary>
    /// The exception that indicates an error from remote RPC endpoint.
    /// </summary>
#if NET45
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

#if NET45
        protected JsonRpcRemoteException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            Error = (ResponseError)info.GetValue("Error", typeof(ResponseError));
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
