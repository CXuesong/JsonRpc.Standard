using System;
using System.Collections.Generic;
using System.Text;
using JsonRpc.Standard.Server;

namespace JsonRpc.Standard.Server
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
            if (serviceHost == null) throw new ArgumentNullException(nameof(serviceHost));
            ServiceHost = serviceHost;
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
