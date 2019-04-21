using System;
using System.Collections;
using System.Text;
using JsonRpc.Standard.Client;
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
        [JsonConstructor]
        public ResponseError()
        {
            
        }

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

        /// <inheritdoc cref="FromException(Exception,bool)"/>
        public static ResponseError FromException(Exception ex)
        {
            return FromException(ex, false);
        }

        /// <summary>
        /// Instantiates a new <see cref="ResponseError"/> from an existing <see cref="Exception"/>.
        /// </summary>
        /// <param name="ex">The exception containing the error information.</param>
        /// <param name="includesStackTrace">Whether to include <see cref="Exception.StackTrace"/> from the given exception. Note that the stack trace may contain sensitive information.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> is <c>null</c>.</exception>
        public static ResponseError FromException(Exception ex, bool includesStackTrace)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            if (ex is JsonRpcException re && re.Error != null) return re.Error;
            return FromException(ClrExceptionErrorData.FromException(ex, includesStackTrace));
        }

        /// <summary>
        /// Instantiates a new <see cref="ResponseError"/> from an existing <see cref="ClrExceptionErrorData"/>.
        /// </summary>
        /// <param name="errorData">The error information.</param>
        /// <exception cref="ArgumentNullException"><paramref name="errorData"/> is <c>null</c>.</exception>
        public static ResponseError FromException(ClrExceptionErrorData errorData)
        {
            if (errorData == null) throw new ArgumentNullException(nameof(errorData));
            return new ResponseError(JsonRpcErrorCode.UnhandledClrException,
                $"{errorData.ExceptionType}: {errorData.Message}", errorData);
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

        /// <summary>
        /// Deserializes <see cref="Data"/> into the object of specified type.
        /// </summary>
        /// <param name="dataType">The target type of JSON deserialization.</param>
        /// <returns>The deserialized data object.</returns>
        public object GetData(Type dataType)
        {
            return Data?.ToObject(dataType, RpcSerializer.Serializer);
        }

        /// <summary>
        /// Deserializes <see cref="Data"/> into the object of specified type.
        /// </summary>
        /// <typeparam name="T">The target type of JSON deserialization.</typeparam>
        /// <returns>The deserialized data object.</returns>
        public T GetData<T>()
        {
            return Data == null ? default(T) : Data.ToObject<T>(RpcSerializer.Serializer);
        }

        /// <summary>
        /// Serializes the specified object into JSON <see cref="Data"/>.
        /// </summary>
        /// <param name="newData">The source value for JSON serialization.</param>
        public void SetData(object newData)
        {
            Data = newData == null ? null : JToken.FromObject(newData, RpcSerializer.Serializer);
        }
    }

    /// <summary>
    /// A POCO object containing additional CLR exception information
    /// that can be serialized into <see cref="ResponseError"/>.<see cref="ResponseError.Data"/>.
    /// </summary>
    public class ClrExceptionErrorData
    {


        internal static bool MightBeOfThisType(JToken jtoken)
        {
            if (!(jtoken is JObject jobj)) return false;
            if (jobj[nameof(ExceptionType)] == null) return false;
            if (jobj[nameof(Message)] == null) return false;
            return true;
        }

        /// <inheritdoc cref="FromException(Exception, bool)"/>
        public static ClrExceptionErrorData FromException(Exception ex)
        {
            return FromException(ex, false);
        }

        /// <summary>
        /// Initializes a new <see cref="ClrExceptionErrorData"/> from an existing exception.
        /// </summary>
        /// <param name="ex">The exception from which to extract error data.</param>
        /// <param name="includesStackTrace">Whether to set <see cref="StackTrace"/> from the given exception. Note that the stack trace may contain sensitive information.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ex"/> is <c>null</c>.</exception>
        public static ClrExceptionErrorData FromException(Exception ex, bool includesStackTrace)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            var inst = new ClrExceptionErrorData
            {
                ExceptionType = ex.GetType().FullName,
                Message = ex.Message,
                Data = ex.Data,
                HResult = ex.HResult,
                HelpLink = ex.HelpLink,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException == null ? null : FromException(ex.InnerException, includesStackTrace)
            };
            // Consider extract such special behaviors into a interface,
            // and let Exception-implementor implement it.
            if (ex is JsonRpcRemoteException re && re.InnerException == null && re.RemoteException != null)
            {
                // For JsonRpcRemoteException, we treat RemoteException as InnerException,
                // if possible. This can be helpful to maintain as much information as we can,
                // especially when we are relaying JSON RPC operations through the channels.
                inst.InnerException = re.RemoteException;
            }

            return inst;
        }

        public string ExceptionType { get; set; }

        public string Message { get; set; }

        public int HResult { get; set; }

        public IDictionary Data { get; set; }

        public string HelpLink { get; set; }

        public string StackTrace { get; set; }

        public ClrExceptionErrorData InnerException { get; set; }

        /// <summary>
        /// Gets the root cause of the exception.
        /// This method is similar to <see cref="Exception"/>.<see cref="Exception.GetBaseException"/>.
        /// </summary>
        public ClrExceptionErrorData GetBaseException()
        {
            var cur = this;
            while (cur.InnerException != null)
            {
                cur = cur.InnerException;
            }
            return cur;
        }

        internal void ToString(StringBuilder sb, int indention)
        {
            if (indention > 0)
            {
                sb.Append('-', indention);
                sb.Append("> ");
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
