using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard.Contracts
{

    /// <summary>
    /// Defines methods to convert CLR method parameters into JSON RPC request parameters.
    /// </summary>
    public interface IJsonRpcRequestMarshaler
    {

        /// <summary>
        /// Marshals the specified parameter values into JSON used as Request.params value.
        /// </summary>
        /// <param name="parameters">The parameters of the method.</param>
        /// <param name="values">The values of the method. <c>null</c> is treated the same as empty array.</param>
        /// <returns>The marshaled parameter value object or array or <c>null</c> for Request.params to be neglected.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
        MarshaledRequestParameters MarshalParameters(IList<JsonRpcParameter> parameters, IList values);

    }

    public class NamedRequestMarshaler : IJsonRpcRequestMarshaler
    {

        public MarshaledRequestParameters MarshalParameters(IList<JsonRpcParameter> parameters, IList values)
        {
            var ct = CancellationToken.None;
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            // Parse parameters
            JObject jargs = null;
            if (parameters.Count > 0)
            {
                jargs = new JObject();
                // Parameter check
                for (int i = 0; i < parameters.Count; i++)
                {
                    var argv = i < values.Count ? values[i] : Type.Missing;
                    var thisParam = parameters[i];
                    if (argv == Type.Missing)
                    {
                        if (!thisParam.IsOptional)
                            throw new ArgumentException($"Parameter \"{thisParam}\" is required.",
                                nameof(values));
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
            return new MarshaledRequestParameters(jargs, ct);
        }
    }

}
