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

        /// <summary>
        /// Converts a string containing JSON RPC message into <see cref="Message"/>.
        /// </summary>
        /// <param name="jsonContent">JSON content.</param>
        /// <returns>A subclass of <see cref="Message"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="jsonContent"/> doesn't contain valid JSON RPC message.</exception>
        public static Message FromJson(string jsonContent)
        {
            try
            {
                return RpcSerializer.DeserializeMessage(jsonContent);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is JsonSerializationException)
            {
                throw new ArgumentException("jsonContent doesn't contain valid JSON RPC message.", nameof(jsonContent));
            }
        }

        /// <summary>
        /// Gets the JSON representation of the message.
        /// </summary>
        public override string ToString()
        {
            return RpcSerializer.SerializeMessage(this);
        }
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
        [JsonProperty("params", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public JToken Parameters { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }

    /// <summary>
    /// An <see cref="Message" /> implementation representing a JSON-RPC request.
    /// </summary>
    public sealed class RequestMessage : GeneralRequestMessage
    {

        public RequestMessage() : this(MessageId.Empty, null, null)
        {
        }

        public RequestMessage(MessageId id) : this(id, null, null)
        {
        }

        public RequestMessage(MessageId id, string method) : this(id, method, null)
        {
        }

        public RequestMessage(MessageId id, string method, JToken parameters) : base(method, parameters)
        {
            Id = id;
        }

        /// <summary>
        /// A unique ID given to the request/response session. The request creator is responsible for assigning this value.
        /// </summary>
        [JsonProperty]
        public MessageId Id { get; set; }
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

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage() : this(MessageId.Empty, null, null)
        {

        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage(MessageId id) : this(id, null, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance that indicates success.
        /// </summary>
        public ResponseMessage(MessageId id, JToken result) : this(id, result, null)
        {
        }


        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance that indicates error.
        /// </summary>
        public ResponseMessage(MessageId id, ResponseError error) : this(id, null, error)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ResponseMessage" /> instance.
        /// </summary>
        public ResponseMessage(MessageId id, JToken result, ResponseError error)
        {
            Id = id;
            Result = result;
            Error = error;
        }

        /// <summary>
        /// A unique ID assigned to the request/response session. The request creator is responsible for this value.
        /// </summary>
        [JsonProperty("id")]
        public MessageId Id { get; set; }

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

    /// <summary>
    /// The message id in JSON RPC requests.
    /// </summary>
    [JsonConverter(typeof(MessageIdJsonConverter))]
    public struct MessageId : IEquatable<MessageId>
    {
        // null, string, int, or long

        /// <summary>
        /// The underlying value of the Id.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Represents an empty MessageId.
        /// </summary>
        public static readonly MessageId Empty = default(MessageId);

        /// <summary>
        /// Constructs a new instance from an underlying id value.
        /// </summary>
        /// <param name="id">Either null, string, or integer is acceptable.</param>
        public MessageId(object id)
        {
            switch (id)
            {
                case null:
                    Value = null;
                    break;
                case int i:
                    Value = i;
                    break;
                case short si:
                    Value = (int) si;
                    break;
                case ushort usi:
                    Value = (int) usi;
                    break;
                case byte bi:
                    Value = (int) bi;
                    break;
                case sbyte sbi:
                    Value = (int) sbi;
                    break;
                case uint ui:
                    // Shrink the data type, if necessary.
                    if (ui <= int.MaxValue)
                        Value = (int) ui;
                    else
                        Value = (long) ui;
                    break;
                case long l:
                    // Shrink the data type, if necessary.
                    if (l >= int.MinValue && l <= int.MaxValue)
                        Value = (int) l;
                    else
                        Value = l;
                    break;
                case ulong ul:
                    if (ul <= int.MaxValue)
                        Value = (int) ul;
                    else
                        Value = (long) ul; // Overflow might happen
                    break;
                case string s:
                    Value = s;
                    break;
                case MessageId id1:
                    Value = id1.Value;
                    break;
                default:
                    throw new ArgumentException("Id should be either null, string, or integer.", nameof(id));
            }
        }

        private static Exception MakeInvalidCastException()
        {
            return new InvalidOperationException("Specified MessageId cannot be cast into the target type.");
        }

        public static implicit operator MessageId(string str)
        {
            return new MessageId(str);
        }

        public static implicit operator MessageId(int x)
        {
            return new MessageId(x);
        }

        public static implicit operator MessageId(long x)
        {
            return new MessageId(x);
        }

        public static explicit operator string(MessageId id)
        {
            if (id.Value is string s) return s;
            throw MakeInvalidCastException();
        }

        public static explicit operator int(MessageId id)
        {
            if (id.Value is int i) return i;
            if (id.Value is long l) return (int) l;
            throw MakeInvalidCastException();
        }

        public static explicit operator long(MessageId id)
        {
            if (id.Value is int i) return i;
            if (id.Value is long l) return l;
            throw MakeInvalidCastException();
        }

        /// <inheritdoc />
        public override string ToString() => Value?.ToString();

        /// <inheritdoc />
        public bool Equals(MessageId other)
        {
            return Equals(Value, other.Value);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MessageId && Equals((MessageId) obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        /// <inheritdoc />
        public static bool operator ==(MessageId left, MessageId right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc />
        public static bool operator !=(MessageId left, MessageId right)
        {
            return !left.Equals(right);
        }
    }

    public class MessageIdJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            var id = (MessageId) value;
            writer.WriteValue(id.Value);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType != typeof(MessageId) && objectType != typeof(object)) throw new NotSupportedException();
            if (reader.TokenType == JsonToken.Integer)
                return new MessageId(Convert.ToInt64(reader.Value));
            return new MessageId(Convert.ToString(reader.Value));
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(MessageId);
        }
    }
}
