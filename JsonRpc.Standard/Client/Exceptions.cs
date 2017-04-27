using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;

namespace JsonRpc.Standard.Client
{
    /// <summary>
    /// The exception that indicates the violation of a server/client-side RPC contract.
    /// </summary>
    public class JsonRpcContractException : Exception
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

        /// <summary>
        /// Gets the JSON RPC message that caused this exception.
        /// </summary>
        public Message RpcMessage { get; }
    }

    /// <summary>
    /// The exception that indicates an error from remote RPC endpoint.
    /// </summary>
    public class JsonRpcRemoteException : Exception
    {
        public JsonRpcRemoteException(string message, Exception innerException)
            : this(message, null, innerException)
        {
        }

        public JsonRpcRemoteException(string message, ResponseError error, Exception innerException)
            : base(message, innerException)
        {
            Error = error;
            if (error?.Code == (int) JsonRpcErrorCode.UnhandledClrException)
            {
                RemoteException = error.GetData<ClrExceptionErrorData>();
            }
        }

        public JsonRpcRemoteException(ResponseError error)
            : this(error?.Message, error, null)
        {

        }

        /// <summary>
        /// The JSON RPC Error object that raises the exception.
        /// </summary>
        public ResponseError Error { get; }

        /// <summary>
        /// Remote CLR exception data, if available.
        /// </summary>
        public ClrExceptionErrorData RemoteException { get; }

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
