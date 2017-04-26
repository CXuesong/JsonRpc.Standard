using System.Collections.Generic;
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
    public interface IRpcMethodBinder
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

    internal class RpcMethodBinder : IRpcMethodBinder
    {

        public static readonly RpcMethodBinder Default = new RpcMethodBinder();

        /// <inheritdoc />
        public JsonRpcMethod TryBindToMethod(ICollection<JsonRpcMethod> candidates, RequestContext context)
        {
            //TODO Support array as params
            if (context.Request.Parameters != null && context.Request.Parameters.Type == JTokenType.Array) return null;
            JsonRpcMethod firstMatch = null;
            Dictionary<string, JToken> requestProp = null;
            foreach (var m in candidates)
            {
                if (!m.AllowExtensionData && context.Request.Parameters != null)
                {
                    // Strict match
                    requestProp = ((JObject) context.Request.Parameters).Properties()
                        .ToDictionary(p => p.Name, p => p.Value);
                }
                foreach (var p in m.Parameters)
                {
                    var jp = context.Request.Parameters?[p.ParameterName];
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
    }
}
