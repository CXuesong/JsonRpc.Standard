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
    }
}