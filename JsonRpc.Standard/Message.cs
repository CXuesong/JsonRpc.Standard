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

        internal GeneralRequestMessage(string method, JToken paramsValue)
        {
            Method = method;
            Params = paramsValue;
        }

        /// <summary>
        /// The method to invoke on the receiver.
        /// </summary>
        [JsonProperty]
        public string Method { get; set; }

        /// <summary>
        /// A <see cref="JObject" /> representing parameters for the method.
        /// </summary>
        /// <remarks>This member MAY be omitted (null).</remarks>
        [JsonProperty]
        public JToken Params { get; set; }
    }

    /// <summary>
    /// An <see cref="Message" /> implementation representing a JSON-RPC request.
    /// </summary>
    public sealed class RequestMessage : GeneralRequestMessage
    {
        private object _Id;

        public RequestMessage() : this(0, null, null)
        {
        }

        public RequestMessage(object id) : this(id, null, null)
        {
        }

        public RequestMessage(object id, string method) : this(id, method, null)
        {
        }

        public RequestMessage(object id, string method, JToken paramsValue) : base(method, paramsValue)
        {
            Id = id;
        }

        /// <summary>
        /// A unique ID given to the request/response session. The request creator is responsible for assigning this value.
        /// </summary>
        [JsonProperty]
        public object Id
        {
            get { return _Id; }
            set
            {
                if (!Utility.ValidateRequestId(value))
                    throw new ArgumentException("Id should be either null, string, or int.", nameof(value));
                _Id = value;
            }
        }
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

        public NotificationMessage(string method, JToken paramsValue) : base(method, paramsValue)
        {

        }
    }

    /// <summary>
    /// An <see cref="Message" /> implementation representing a JSON-RPC response.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ResponseMessage : Message
    {
        private object _Id;

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage() : this(-1, null, null)
        {

        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage(object id, object result) : this(id, result, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage(object id, object result, ResponseError error)
        {
            Id = id;
            SetResult(result);
            Error = error;
        }

        /// <summary>
        /// A unique ID assigned to the request/response session. The request creator is responsible for this value.
        /// </summary>
        [JsonProperty]
        public object Id
        {
            get { return _Id; }
            set
            {
                if (!Utility.ValidateRequestId(value))
                    throw new ArgumentException("Id should be either null, string, or int.", nameof(value));
                _Id = value;
            }
        }

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
