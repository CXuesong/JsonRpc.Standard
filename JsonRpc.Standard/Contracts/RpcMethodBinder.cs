using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JsonRpc.Standard.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Defines method to choose the best match among a set of RPC methods according to the JSON RPC request.
    /// </summary>
    public interface IJsonRpcMethodBinder
    {
        /// <summary>
        /// Resolves the target RPC method from the JSON RPC request.
        /// </summary>
        /// <param name="candidates">The methods to choose from.</param>
        /// <param name="context">The request context.</param>
        /// <returns>Target RPC method information, or <c>null</c> if no suitable method exists.</returns>
        /// <exception cref="AmbiguousMatchException">More than one method is found with that suits the specified request.</exception>
        JsonRpcMethod TryBindToMethod(ICollection<JsonRpcMethod> candidates, RequestContext context);
    }

    internal class JsonRpcMethodBinder : IJsonRpcMethodBinder
    {

        public static readonly JsonRpcMethodBinder Default = new JsonRpcMethodBinder();

        /// <inheritdoc />
        public JsonRpcMethod TryBindToMethod(ICollection<JsonRpcMethod> candidates, RequestContext context)
        {
            // context.Request.Parameters can be: {}, [], null (JValue), null
            // Parameters MAY be omitted.
            if (context.Request.Parameters == null || context.Request.Parameters.Type == JTokenType.Null)
                return TryBindToParameterlessMethod(candidates);
            // by-name
            if (context.Request.Parameters is JObject paramsObj)
                return TryBindToMethod(candidates, paramsObj);
            // by-position
            if (context.Request.Parameters is JArray paramsArray)
                return TryBindToMethod(candidates, paramsArray);
            // Other invalid cases, e.g., naked JValue.
            return null;
        }

        private JsonRpcMethod TryBindToParameterlessMethod(ICollection<JsonRpcMethod> candidates)
        {
            JsonRpcMethod firstMatch = null;
            foreach (var m in candidates)
            {
                if (m.Parameters.Count == 0 || m.Parameters.All(p => p.IsOptional))
                {
                    if (firstMatch != null) throw new AmbiguousMatchException();
                    firstMatch = m;
                }
            }
            return firstMatch;
        }

        private JsonRpcMethod TryBindToMethod(ICollection<JsonRpcMethod> candidates, JObject paramsObj)
        {
            Debug.Assert(paramsObj != null);
            JsonRpcMethod firstMatch = null;
            Dictionary<string, JToken> requestProp = null;
            foreach (var m in candidates)
            {
                if (!m.AllowExtensionData)
                {
                    // Strict match
                    requestProp = paramsObj.Properties().ToDictionary(p => p.Name, p => p.Value);
                }
                foreach (var p in m.Parameters)
                {
                    var jp = paramsObj?[p.ParameterName];
                    if (jp == null)
                    {
                        if (!p.IsOptional) goto NEXT;
                        else continue;
                    }
                    if (!p.MatchJTokenType(jp.Type)) goto NEXT;
                    requestProp?.Remove(p.ParameterName);
                }
                // Check whether we have extra parameters.
                if (requestProp != null && requestProp.Count > 0) goto NEXT;
                if (firstMatch != null) throw new AmbiguousMatchException();
                firstMatch = m;
                NEXT:
                ;
            }
            return firstMatch;
        }

        private JsonRpcMethod TryBindToMethod(ICollection<JsonRpcMethod> candidates, JArray paramsArray)
        {
            Debug.Assert(paramsArray != null);
            JsonRpcMethod firstMatch = null;
            foreach (var m in candidates)
            {
                if (!m.AllowExtensionData && paramsArray.Count > m.Parameters.Count) goto NEXT;
                for (var i = 0; i < m.Parameters.Count; i++)
                {
                    var param = m.Parameters[i];
                    var jparam = i < paramsArray.Count ? paramsArray[i] : null;
                    if (jparam == null)
                    {
                        if (!param.IsOptional) goto NEXT;
                        else continue;
                    }
                    if (!param.MatchJTokenType(jparam.Type)) goto NEXT;
                }
                if (firstMatch != null) throw new AmbiguousMatchException();
                firstMatch = m;
                NEXT:
                ;
            }
            return firstMatch;
        }
    }
}
