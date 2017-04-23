using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents the base abstract JSON-RPC message.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Message
    {
        /// <summary>
        /// Creates a new <see cref="Message" /> instance.
        /// </summary>
        internal Message()
        {
            Version = "2.0";
        }

        /// <summary>
        /// The version of the JSON-RPC specification in use.
        /// </summary>
        /// <remarks>
        /// <note type="note">This property is not used in version 1.0 of the JSON-RPC specification. As of version 2.0, the value should always be "2.0".</note>
        /// </remarks>
        [JsonProperty("jsonrpc")]
        public string Version { get; set; }
    }

    public abstract class GeneralRequestMessage : Message
    {
        internal GeneralRequestMessage() : this(null, null)
        {
            
        }

        internal GeneralRequestMessage(string method) : this(method, null)
        {
            
        }

        internal GeneralRequestMessage(string method, object paramsValue)
        {
            Method = method;
            SetParams(paramsValue);
        }

        /// <summary>
        /// The method to invoke on the receiver.
        /// </summary>
        [JsonProperty]
        public string Method { get; set; }

        /// <summary>
        /// A <see cref="JObject" /> representing parameters for the method.
        /// </summary>
        [JsonProperty]
        public JToken Params { get; set; }

        public object GetParams(Type paramsType)
        {
            return Params?.ToObject(paramsType, RpcSerializer.Serializer);
        }

        public T GetParams<T>()
        {
            return Params == null ? default(T) : Params.ToObject<T>(RpcSerializer.Serializer);
        }

        public void SetParams(object newParams)
        {
            Params = newParams == null ? null : JToken.FromObject(newParams, RpcSerializer.Serializer);
        }
    }

    /// <summary>
    /// An <see cref="Message" /> implementation representing a JSON-RPC request.
    /// </summary>
    public sealed class RequestMessage : GeneralRequestMessage
    {
        public RequestMessage() : this(0, null, null)
        {
        }

        public RequestMessage(int id) : this(id, null, null)
        {
        }

        public RequestMessage(int id, string method) : this(id, method, null)
        {
        }

        public RequestMessage(int id, string method, object paramsValue) : base(method, paramsValue)
        {
            Id = id;
        }

        /// <summary>
        /// A unique ID given to the request/response session. The request creator is responsible for assigning this value.
        /// </summary>
        [JsonProperty]
        public int Id { get; set; }
    }

    /// <summary>
    /// An <see cref="Message" /> implementation representing a JSON-RPC notification.
    /// </summary>
    public sealed class NotificationMessage : GeneralRequestMessage
    {
        public NotificationMessage() : this(null, null)
        {
        }

        public NotificationMessage(string method) : this(method, null)
        {
        }

        public NotificationMessage(string method, object paramsValue) : base(method, paramsValue)
        {

        }
    }

    /// <summary>
    /// An <see cref="Message" /> implementation representing a JSON-RPC response.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ResponseMessage : Message
    {
        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage() : this(-1, null, null)
        {

        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage(int id, object result) : this(id, result, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage(int id, object result, ResponseError error)
        {
            Id = id;
            SetResult(result);
            Error = error;
        }

        /// <summary>
        /// A unique ID assigned to the request/response session. The request creator is responsible for this value.
        /// </summary>
        [JsonProperty]
        public int Id { get; set; }

        // TODO a response should EITHER contain result or error node, not BOTH.

        /// <summary>
        /// The error that occurred while processing the request.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ResponseError Error { get; set; }

        /// <summary>
        /// An object representing the result of processing the request.
        /// </summary>
        [JsonProperty]
        public JToken Result { get; set; }

        public object GetResult(Type resultType)
        {
            return Result?.ToObject(resultType, RpcSerializer.Serializer);
        }

        public T GetResult<T>()
        {
            return Result == null ? default(T) : Result.ToObject<T>(RpcSerializer.Serializer);
        }

        public void SetResult(object result)
        {
            Result = result == null ? null : JToken.FromObject(result, RpcSerializer.Serializer);
        }
    }
}
