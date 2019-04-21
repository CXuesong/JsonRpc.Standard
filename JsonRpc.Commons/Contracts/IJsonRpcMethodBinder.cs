using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using JsonRpc.Server;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Contracts
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
        /// <exception cref="ArgumentNullException">Either <paramref name="candidates"/> or <paramref name="context"/> is null.</exception>
        /// <exception cref="AmbiguousMatchException">More than one method is found with that suits the specified request.</exception>
        JsonRpcMethod TryBindToMethod(ICollection<JsonRpcMethod> candidates, RequestContext context);

        /// <summary>
        /// Binds the parameters contained in the specified JSON RPC request to the specified a parameter list.
        /// </summary>
        /// <param name="parameters">The target JSON RPC method parameters.</param>
        /// <param name="context">The request context containing the parameters to be converted.</param>
        /// <returns>An array of parameter values that will be used to invoke the actual CLR method.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="parameters"/> or <paramref name="context"/> is null.</exception>
        object[] BindParameters(IList<JsonRpcParameter> parameters, RequestContext context);
    }

    internal class JsonRpcMethodBinder : IJsonRpcMethodBinder
    {

        public static readonly JsonRpcMethodBinder Default = new JsonRpcMethodBinder();

        private static readonly object[] emptyObjectArray = { };

        /// <inheritdoc />
        public JsonRpcMethod TryBindToMethod(ICollection<JsonRpcMethod> candidates, RequestContext context)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var contextParams = context.Request.Parameters;
            // context.Request.Parameters can be: {}, [], null (JValue), null
            // Parameters MAY be omitted.
            if (contextParams == null || contextParams.Type == JTokenType.Null)
                return TryBindToParameterlessMethod(candidates);
            // by-name
            if (contextParams is JObject paramsObj)
                return TryBindToMethod(candidates, paramsObj);
            // by-position
            if (contextParams is JArray paramsArray)
                return TryBindToMethod(candidates, paramsArray);
            // Other invalid cases, e.g., naked JValue.
            return null;
        }

        /// <inheritdoc />
        public object[] BindParameters(IList<JsonRpcParameter> parameters, RequestContext context)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (context == null) throw new ArgumentNullException(nameof(context));
            // Assume the parameters and context corresponds here.
            var contextParams = context.Request.Parameters;
            if (parameters.Count == 0) return emptyObjectArray;
            var argv = new object[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                // Resolve cancellation token
                if (parameters[i].ParameterType == typeof(CancellationToken))
                {
                    argv[i] = context.CancellationToken;
                    continue;
                }
                // Resolve other parameters, considering the optional
                JToken jarg;
                switch (contextParams)
                {
                    case JObject _:
                        jarg = contextParams[parameters[i].ParameterName];
                        break;
                    case JArray arr:
                        jarg = i < arr.Count ? arr[i] : null;
                        break;
                    default:
                        jarg = null;
                        break;
                }
                // The argument is missing (or undefined)
                if (jarg == null || jarg.Type == JTokenType.Undefined)
                {
                    if (parameters[i].IsOptional)
                        argv[i] = parameters[i].DefaultValue;
                    else
                        throw new InvalidOperationException($"Required parameter \"{parameters[i].ParameterName}\" is missing.");
                }
                else
                {
                    argv[i] = parameters[i].Converter.JsonToValue(jarg, parameters[i].ParameterType);
                }
            }
            return argv;
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
                    var jp = paramsObj[p.ParameterName];
                    if (jp == null || jp.Type == JTokenType.Undefined)
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
                    if (jparam == null || jparam.Type == JTokenType.Undefined)
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
