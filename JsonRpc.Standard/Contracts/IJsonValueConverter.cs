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

        private readonly JsonSerializer serializer;

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
    /// Predefined <see cref="IJsonValueConverter"/> implementations.
    /// </summary>
    public static class JsonValueConverters
    {

        private static readonly JsonSerializer camelCaseSerializer =
            new JsonSerializer { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        /// <summary>
        /// A default JSON converter using the default settings on JsonSerializer.
        /// </summary>
        public static IJsonValueConverter Default { get; } = new JsonValueConverter();

        /// <summary>
        /// A JSON converter that maps all the member names into camelcase JSON property names.
        /// </summary>
        public static IJsonValueConverter CamelCase { get; } = new JsonValueConverter(camelCaseSerializer);
    }
}
