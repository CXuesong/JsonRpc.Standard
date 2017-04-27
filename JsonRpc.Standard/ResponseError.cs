using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Error codes, including those who are defined by the JSON-RPC 2.0 specification.
    /// </summary>
    /// <remarks>Error codes in the range -32000~-32029 are reserved for JsonRpc.Standard.</remarks>
    public enum JsonRpcErrorCode
    {
        /// <summary>
        /// Internal JSON-RPC error. (JSON-RPC)
        /// </summary>
        InternalError = -32603,

        /// <summary>
        /// Invalid method parameter(s). (JSON-RPC)
        /// </summary>
        InvalidParams = -32602,

        /// <summary>
        /// The JSON sent is not a valid Request object. (JSON-RPC)
        /// </summary>
        InvalidRequest = -32600,

        /// <summary>
        /// The method does not exist / is not available. (JSON-RPC)
        /// </summary>
        MethodNotFound = -32601,

        /// <summary>
        /// Invalid JSON was received by the server. An error occurred on the server while parsing the JSON text. (JSON-RPC)
        /// </summary>
        ParseError = -32700,

        /// <summary>
        /// There is unhandled CLR exception occurred during the process of request. (JsonRpc.Standard)
        /// </summary>
        UnhandledClrException = -32010,
    }

    /// <summary>
    /// JSON RPC Error contract object.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ResponseError
    {
        public ResponseError(int code, string message) : this(code, message, null)
        {
        }

        public ResponseError(int code, string message, object data)
        {
            Code = code;
            Message = message;
            SetData(data);
        }

        public ResponseError(JsonRpcErrorCode code, string message)
            : this(code, message, null)
        {
        }

        public ResponseError(JsonRpcErrorCode code, string message, object data)
            : this((int) code, message, data)
        {

        }

        public static ResponseError FromException(Exception ex)
        {
            if (ex is JsonRpcException re && re.Error != null) return re.Error;
            return new ResponseError(JsonRpcErrorCode.UnhandledClrException, $"{ex.GetType()}: {ex.Message}",
                ClrExceptionErrorData.FromException(ex));
        }

        [JsonProperty]
        public int Code { get; set; }

        [JsonProperty]
        public string Message { get; set; }

        /// <summary>
        /// A <see cref="JToken" /> representing parameters for the method.
        /// </summary>
        [JsonProperty]
        public JToken Data { get; set; }

        public object GetData(Type dataType)
        {
            return Data?.ToObject(dataType, RpcSerializer.Serializer);
        }

        public T GetData<T>()
        {
            return Data == null ? default(T) : Data.ToObject<T>(RpcSerializer.Serializer);
        }

        public void SetData(object newData)
        {
            Data = newData == null ? null : JToken.FromObject(newData, RpcSerializer.Serializer);
        }
    }

    public class ClrExceptionErrorData
    {
        public static ClrExceptionErrorData FromException(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            return new ClrExceptionErrorData
            {
                ExceptionType = ex.GetType().FullName,
                Data = ex.Data,
                HResult = ex.HResult,
                HelpLink = ex.HelpLink,
                StackTrace = null,
                InnerException = ex.InnerException == null ? null : FromException(ex.InnerException)
            };
        }

        public string ExceptionType { get; set; }

        public string Message { get; set; }

        public int HResult { get; set; }

        public IDictionary Data { get; set; }

        public string HelpLink { get; set; }

        public string StackTrace { get; set; }

        public ClrExceptionErrorData InnerException { get; set; }

        private void ToString(StringBuilder sb, int indention)
        {
            if (indention > 0)
            {
                sb.Append('-', indention);
                sb.Append(" ");
            }
            sb.AppendFormat("{0}: {1}", ExceptionType, Message);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(StackTrace)) sb.AppendLine(StackTrace);
            if (InnerException != null)
            {
                InnerException.ToString(sb, indention + 1);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(sb, 0);
            return sb.ToString();
        }
    }
}
