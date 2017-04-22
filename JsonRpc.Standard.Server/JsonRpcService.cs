using System;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Base class for providing JSON RPC service.
    /// </summary>
    public class JsonRpcService
    {
        /// <summary>
        /// Gets or sets the <see cref="RequestContext"/> of current request.
        /// </summary>
        public RequestContext RequestContext { get; set; }
    }

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
    }
}
