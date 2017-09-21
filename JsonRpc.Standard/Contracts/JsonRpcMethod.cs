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

        public IJsonRpcMethodInvoker Invoker { get; set; }

        ///// <summary>
        ///// Whether the method is cancellable.
        ///// </summary>
        //public bool Cancellable { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{MethodName}({Parameters.Count})";
        }

        internal MarshaledRequest Marshal(IList arguments)
        {
            var ct = CancellationToken.None;
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
                        ct = (CancellationToken) argv;
                        continue;
                    }
                    var value = thisParam.Converter.ValueToJson(argv);
                    jargs.Add(thisParam.ParameterName, value);
                }
            }
            return new MarshaledRequest(new RequestMessage(this.MethodName, jargs), ct);
        }
    }

    internal struct MarshaledRequest
    {
        public MarshaledRequest(RequestMessage message, CancellationToken cancellationToken)
        {
            Message = message;
            CancellationToken = cancellationToken;
        }

        public RequestMessage Message { get; }

        public CancellationToken CancellationToken { get; }
    }
}