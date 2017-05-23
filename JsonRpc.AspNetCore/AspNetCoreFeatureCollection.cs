using System;
using System.Collections.Generic;
using System.Text;
using JsonRpc.Standard.Server;
using Microsoft.AspNetCore.Http;

namespace JsonRpc.AspNetCore
{
    internal class AspNetCoreFeatureCollection : IFeatureCollection
    {
        public HttpContext Context { get; }

        private IAspNetCoreFeature feature;

        public AspNetCoreFeatureCollection(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            Context = context;
        }

        /// <inheritdoc />
        public object Get(Type featureType)
        {
            if (featureType == typeof(IAspNetCoreFeature))
            {
                if (feature == null) feature = new AspNetCoreFeature(Context);
                return feature;
            }
            return null;
        }

        /// <inheritdoc />
        public void Set(Type featureType, object instance)
        {
            throw new NotSupportedException();
        }
    }
}
