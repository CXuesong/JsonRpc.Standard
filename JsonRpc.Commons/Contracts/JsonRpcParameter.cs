using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Contracts
{
    /// <summary>
    /// Provides information to map an argument in JSON RPC method to a CLR method argument.
    /// </summary>
    /// <remarks>
    /// The "parameter" means either an input parameter of the method, or the function return value.
    /// </remarks>
    public sealed class JsonRpcParameter
    {

        private IJsonValueConverter _Converter = JsonValueConverter.Default;

        /// <summary>
        /// The parameter name used in JSON.
        /// </summary>
        /// <remarks>
        /// If this parameter represents the return value of the function,
        /// property value should be <c>null</c>.
        /// </remarks>
        public string ParameterName { get; set; }

        /// <summary>
        /// Whether the parameter is optional.
        /// </summary>
        /// <remarks>
        /// Parameters with certain types (e.g. <see cref="CancellationToken"/>)
        /// are always treated as optional.
        /// </remarks>
        public bool IsOptional { get; set; }

        /// <summary>
        /// The default value for the specified optional parameter.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Whether the parameter is a <see cref="Task"/> or <c>Task&lt;ParameterType&gt;</c> instead of ParameterType itself.
        /// </summary>
        public bool IsTask { get; set; }

        /// <summary>
        /// The bare type of the parameter.
        /// </summary>
        public Type ParameterType { get; set; }

        internal bool MatchJTokenType(JTokenType type)
        {
            if (ParameterType == typeof(JToken)) return true;
            var ti = ParameterType.GetTypeInfo();
            switch (type)
            {
                case JTokenType.Object:
                    return !(ti.IsPrimitive || ParameterType == typeof(string));
                case JTokenType.Array:
                    return typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(ti);
                case JTokenType.Boolean:
                    return ParameterType == typeof(bool) || ParameterType == typeof(bool?);
                case JTokenType.Integer:
                    if (ti.IsEnum) return true;
                    if (ParameterType == typeof(MessageId)) return true;
                    goto case JTokenType.Float;
                case JTokenType.Float:
                    return ParameterType == typeof(byte) || ParameterType == typeof(byte?)
                           || ParameterType == typeof(short) || ParameterType == typeof(short?)
                           || ParameterType == typeof(int) || ParameterType == typeof(int?)
                           || ParameterType == typeof(long) || ParameterType == typeof(long?)
                           || ParameterType == typeof(sbyte) || ParameterType == typeof(sbyte?)
                           || ParameterType == typeof(ushort) || ParameterType == typeof(ushort?)
                           || ParameterType == typeof(uint) || ParameterType == typeof(uint?)
                           || ParameterType == typeof(ulong) || ParameterType == typeof(ulong?)
                           || ParameterType == typeof(float) || ParameterType == typeof(float?)
                           || ParameterType == typeof(double) || ParameterType == typeof(double?);
                case JTokenType.Date:
                case JTokenType.TimeSpan:
                case JTokenType.Uri:
                case JTokenType.Guid:
                case JTokenType.String:
                    // They are all JSON string¡­
                    return !ti.IsPrimitive
                           || ParameterType == typeof(char)
                           || ParameterType == typeof(char?);
                case JTokenType.Null:
                    if (ParameterType == typeof(MessageId)) return true;
                    return !ParameterType.GetTypeInfo().IsValueType
                           || ParameterType.IsConstructedGenericType &&
                           ParameterType.GetGenericTypeDefinition() == typeof(Nullable<>);
                default:
                    return false;
            }
        }

        /// <summary>
        /// The serializer used to convert the parameter.
        /// </summary>
        public IJsonValueConverter Converter
        {
            get => _Converter;
            set => _Converter = value ?? JsonValueConverter.Default;
        }

        /// <inheritdoc />
        public override string ToString() => ParameterType + " " + ParameterName;

    }
}