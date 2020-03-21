using System;

namespace JsonRpc.Server
{
    /// <summary>
    /// Abstract class for receiving request from somewhere,
    /// and invoking <see cref="IJsonRpcServiceHost"/> with appropriate information.
    /// </summary>
    public abstract class JsonRpcServerHandler
    {
        private IFeatureCollection _DefaultFeatures;

        public JsonRpcServerHandler(IJsonRpcServiceHost serviceHost)
        {
            ServiceHost = serviceHost ?? throw new ArgumentNullException(nameof(serviceHost));
        }

        /// <summary>
        /// Gets the underlying <see cref="IJsonRpcServiceHost"/>.
        /// </summary>
        public IJsonRpcServiceHost ServiceHost { get; }

        /// <summary>
        /// Gets/sets the default features applied to the <see cref="RequestContext"/>.
        /// </summary>
        public IFeatureCollection DefaultFeatures
        {
            get
            {
                if (_DefaultFeatures == null) _DefaultFeatures = new FeatureCollection();
                return _DefaultFeatures;
            }
            set { _DefaultFeatures = value; }
        }
    }
}
