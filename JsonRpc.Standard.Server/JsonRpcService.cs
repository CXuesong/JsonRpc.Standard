using System;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Base class for providing JSON RPC service.
    /// </summary>
    public class JsonRpcService
    {
        public ISession Context;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    sealed class JsonRpcMethodAttribute : Attribute
    {
        public JsonRpcMethodAttribute() : this(null)
        {
        }

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
