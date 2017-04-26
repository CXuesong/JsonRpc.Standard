using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Provides information to map a JSON RPC method to a CLR method.
    /// </summary>
    public sealed class JsonRpcMethod
    {
        public JsonRpcMethod()
        {

        }

        public Type ServiceType { get; set; }

        public string MethodName { get; set; }

        public bool IsNotification { get; set; }

        public bool AllowExtensionData { get; set; }

        public IList<JsonRpcParameter> Parameters { get; set; }

        public JsonRpcParameter ReturnParameter { get; set; }

        public IJsonRpcMethodHandler Handler { get; set; }

        internal GeneralRequestMessage Marshal(IList arguments)
        {
            CancellationToken ct = CancellationToken.None;
            // Parse parameters
            JObject jargs = null;
            if (this.Parameters.Count > 0)
            {
                if (arguments == null) throw new ArgumentNullException(nameof(arguments));
                jargs = new JObject();
                // Parameter check
                for (int i = 0; i < this.Parameters.Count; i++)
                {
                    var argv = i < arguments.Count ? arguments[i] : Type.Missing;
                    var thisParam = this.Parameters[i];
                    if (argv == Type.Missing)
                    {
                        if (!thisParam.IsOptional)
                            throw new ArgumentException($"Parameter \"{thisParam}\" is required.",
                                nameof(arguments));
                        continue;
                    }
                    if (thisParam.ParameterType == typeof(CancellationToken))
                    {
                        ct = (CancellationToken)argv;
                        continue;
                    }
                    var value = thisParam.Converter.ValueToJson(argv);
                    jargs.Add(thisParam.ParameterName, value);
                }
            }
            if (this.IsNotification)
                return new NotificationMessage(this.MethodName, jargs) { CancellationToken = ct };
            else
                return new RequestMessage(null, this.MethodName, jargs) { CancellationToken = ct };
        }

        /// <inheritdoc />
        internal object[] UnmarshalArguments(GeneralRequestMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (this.Parameters.Count == 0) return null;
            var argv = new object[this.Parameters.Count];
            for (int i = 0; i < this.Parameters.Count; i++)
            {
                // Resolve cancellation token
                if (this.Parameters[i].ParameterType == typeof(CancellationToken))
                {
                    argv[i] = message.CancellationToken;
                    continue;
                }
                // Resolve other parameters, considering the optional
                var jarg = message.Parameters?[this.Parameters[i].ParameterName];
                if (jarg == null)
                {
                    if (this.Parameters[i].IsOptional)
                        argv[i] = Type.Missing;
                    else if (message is RequestMessage request)
                        throw new JsonRpcException(JsonRpcErrorCode.InvalidParams,
                            $"Required parameter \"{this.Parameters[i].ParameterName}\" is missing for \"{this.MethodName}\".");
                    else
                    {
                        // TODO Logging: Argument missing, but the client do not need a response, so we just ignore the error.
                    }
                }
                else
                {
                    argv[i] = this.Parameters[i].Converter.JsonToValue(jarg, this.Parameters[i].ParameterType);
                }
            }
            return argv;
        }
    }
}