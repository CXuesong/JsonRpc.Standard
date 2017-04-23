using System;
using System.Collections;
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
            if (ex is JsonRpcException re) return FromException(re);
            return new ResponseError(JsonRpcErrorCode.UnhandledClrException, $"{ex.GetType()}: {ex.Message}",
                UnhandledClrExceptionData.FromException(ex));
        }

        public static ResponseError FromException(JsonRpcException ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            return new ResponseError(ex.Code, ex.Message, ex.RpcData);
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

    /// <summary>
    /// This class is for internal use.
    /// </summary>
    internal class UnhandledClrExceptionData
    {
        public static UnhandledClrExceptionData FromException(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            return new UnhandledClrExceptionData
            {
                ExceptionType = ex.GetType().AssemblyQualifiedName,
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

        public UnhandledClrExceptionData InnerException { get; set; }

        public Exception ToException()
        {
            var inner = InnerException?.ToException();
            var type = Type.GetType(ExceptionType);
            if (type != null)
            {
                var inst = (Exception) Activator.CreateInstance(type, Message, inner);
                inst.HelpLink = HelpLink;
                return inst;
            }
            else
            {
                return new Exception($"[{ExceptionType}]: {Message}", inner);
            }
        }
    }
}
