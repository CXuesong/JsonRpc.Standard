using System;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Indicates the method is exposed for JSON RPC invocation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class JsonRpcMethodAttribute : Attribute
    {
        /// <summary>
        /// Creates a default attribute instance.
        /// </summary>
        public JsonRpcMethodAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates an attribute instance.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        public JsonRpcMethodAttribute(string methodName)
        {
            MethodName = methodName;
        }

        /// <summary>
        /// The name of the method. <c>null</c> to use the applied method name.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Used in the client stub. Indicates whether the method is a notification request.
        /// </summary>
        public bool IsNotification { get; set; }

        /// <summary>
        /// Used in the server. Whether allows extra parameters on this method when matching signature.
        /// </summary>
        public bool AllowExtensionData { get; set; }
    }

    /// <summary>
    /// Specifies the 
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue, Inherited = true, AllowMultiple = false)]
    public sealed class JsonRpcParameterAttribute : Attribute
    {
        /// <summary>
        /// Creates a default attribute instance.
        /// </summary>
        public JsonRpcParameterAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates an attribute instance.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        public JsonRpcParameterAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        /// <summary>
        /// The name of the parameter. <c>null</c> to use the applied Parameter name.
        /// </summary>
        public string ParameterName { get; }
    }
}