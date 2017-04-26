using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Used to map JSON RPC method and argument names into CLR counterparts.
    /// </summary>
    /// <remarks>
    /// To decide the naming strategy for the JSON representation of argument CONTENT,
    /// please use <see cref="JsonValueConverter"/> with a customized <see cref="JsonSerializer"/>.
    /// </remarks>
    public class JsonRpcNamingStrategy
    {
        public virtual string GetRpcMethodName(string methodName, bool isSpecified)
        {
            return methodName;
        }

        public virtual string GetRpcParameterName(string parameterName, bool isSpecified)
        {
            return parameterName;
        }
    }

    public static class JsonRpcNamingStrategies
    {

        public static JsonRpcNamingStrategy Default { get; } = new JsonRpcNamingStrategy();

        public static JsonRpcNamingStrategy CamelCase { get; } = new CamelCaseJsonRpcNamingStrategy();

        private class CamelCaseJsonRpcNamingStrategy : JsonRpcNamingStrategy
        {

            private static string ToCamelCase(string s)
            {
                if (string.IsNullOrEmpty(s)) return s;
                if (char.IsUpper(s[0])) return char.ToLowerInvariant(s[0]) + s.Substring(1);
                return s;
            }

            /// <inheritdoc />
            public override string GetRpcMethodName(string methodName, bool isSpecified)
            {
                if (isSpecified) return methodName;
                return ToCamelCase(methodName);
            }

            /// <inheritdoc />
            public override string GetRpcParameterName(string parameterName, bool isSpecified)
            {
                if (isSpecified) return parameterName;
                return ToCamelCase(parameterName);
            }
        }

    }
}
