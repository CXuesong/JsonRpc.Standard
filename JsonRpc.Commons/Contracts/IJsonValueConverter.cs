using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Used to convert value from/to JToken.
    /// </summary>
    public interface IJsonValueConverter
    {
        JToken ValueToJson(object value);

        object JsonToValue(JToken json, Type valueType);
    }

    /// <summary>
    /// A JSON converter based on <see cref="JsonSerializer"/>.
    /// </summary>
    public class JsonValueConverter : IJsonValueConverter
    {

        private static readonly JsonSerializer defaultSerializer = new JsonSerializer();

        internal static readonly CamelCaseJsonValueConverter Default = new CamelCaseJsonValueConverter();

        private readonly JsonSerializer serializer;

        /// <summary>
        /// Initializes a new instance with a default JSON serializer.
        /// </summary>
        public JsonValueConverter() : this(defaultSerializer)
        {

        }

        public JsonValueConverter(JsonSerializer serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            this.serializer = serializer;
        }

        /// <inheritdoc />
        public virtual JToken ValueToJson(object value)
        {
            if (value == null) return JValue.CreateNull();
            return JToken.FromObject(value, serializer);
        }

        /// <inheritdoc />
        public virtual object JsonToValue(JToken json, Type valueType)
        {
            if (valueType == typeof(void)) return null;
            return json?.ToObject(valueType, serializer);
        }
    }

    /// <summary>
    /// A JSON converter based on <see cref="JsonSerializer"/> with <see cref="CamelCasePropertyNamesContractResolver"/>.
    /// </summary>
    public sealed class CamelCaseJsonValueConverter : JsonValueConverter
    {
        private static readonly JsonSerializer camelCaseSerializer =
            new JsonSerializer { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        internal static readonly CamelCaseJsonValueConverter CamelCaseDefault = new CamelCaseJsonValueConverter();

        public CamelCaseJsonValueConverter() : base(camelCaseSerializer)
        {
            
        }
    }
}
