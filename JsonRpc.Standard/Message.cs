using System;
using System.Diagnostics;
using System.Threading;
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

        internal GeneralRequestMessage(string method, JToken parameters)
        {
            Method = method;
            Parameters = parameters;
        }

        /// <summary>
        /// The method to invoke on the receiver.
        /// </summary>
        [JsonProperty("method")]
        public string Method { get; set; }

        /// <summary>
        /// A <see cref="JObject" /> representing parameters for the method.
        /// </summary>
        /// <remarks>This member MAY be omitted (null).</remarks>
        [JsonProperty("params")]
        public JToken Parameters { get; set; }


        public CancellationToken CancellationToken { get; set; }
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

        public RequestMessage(object id, string method, JToken parameters) : base(method, parameters)
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

        public NotificationMessage(string method, JToken parameters) : base(method, parameters)
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
        /// Creates a new <see cref="ResponseMessage" /> instance that indicates success.
        /// </summary>
        public ResponseMessage(object id, JToken result) : this(id, result, null)
        {
        }


        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance that indicates error.
        /// </summary>
        public ResponseMessage(object id, ResponseError error) : this(id, null, error)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage(object id, JToken result, ResponseError error)
        {
            Id = id;
            Result = result;
            Error = error;
        }

        /// <summary>
        /// A unique ID assigned to the request/response session. The request creator is responsible for this value.
        /// </summary>
        [JsonProperty("id")]
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
        // This member MUST NOT exist if there was no error triggered during invocation.
        // The value for this member MUST be an Object as defined in section 5.1.
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public ResponseError Error { get; set; }

        /// <summary>
        /// An object representing the result of processing the request.
        /// </summary>
        /// <remarks>
        /// To compose a valid JSON RPC response, you need to set this property to
        /// the value returned by <see cref="JValue.CreateNull"/>, if the response is
        /// sucess and no other value is to be offered.
        /// </remarks>
        // This member is REQUIRED on success.
        // This member MUST NOT exist if there was an error invoking the method.
        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Result { get; set; }
    }
}
