using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace JsonRpc.Standard
{
    internal static class RpcSerializer
    {
        public static readonly JsonSerializer Serializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        internal static Message DeserializeMessage(string content)
        {
            using (var reader = new StringReader(content))
                return DeserializeMessage(reader);
        }

        // Suppose the reader has an underlying stream of type MemoryStream, so we do
        // no need async methods.
        internal static Message DeserializeMessage(TextReader reader)
        {
            Message message;
            JObject json;
            using (var jreader = new JsonTextReader(reader)) json = JObject.Load(jreader);
            if (json["jsonrpc"] == null)
                throw new ArgumentException("Content is not a valid JSON-RPC message.", nameof(reader));
            if (json["id"] == null)
                message = json.ToObject<NotificationMessage>(Serializer);
            else if (json["method"] == null)
                message = json.ToObject<ResponseMessage>(Serializer);
            else
                message = json.ToObject<RequestMessage>(Serializer);
            return message;
        }

        internal static string SerializeMessage(Message message)
        {
            using (var writer = new StringWriter())
            {
                SerializeMessage(writer, message);
                return writer.ToString();
            }
        }

        internal static void SerializeMessage(TextWriter writer, Message message)
        {
            using (var jwriter = new JsonTextWriter(writer))
            {
                Serializer.Serialize(jwriter, message);
            }
        }
    }
}
