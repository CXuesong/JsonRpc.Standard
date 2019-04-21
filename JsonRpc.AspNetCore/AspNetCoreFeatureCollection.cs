using System;
using System.Collections.Generic;
using System.Text;
using JsonRpc.Server;
using Microsoft.AspNetCore.Http;

namespace JsonRpc.AspNetCore
{
    internal class AspNetCoreFeatureCollection : IFeatureCollection
    {

        private IAspNetCoreFeature feature;

        public AspNetCoreFeatureCollection(IFeatureCollection baseCollection, HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            BaseCollection = baseCollection;
            Context = context;
        }

        public IFeatureCollection BaseCollection { get; }

        public HttpContext Context { get; }

        /// <inheritdoc />
        public object Get(Type featureType)
        {
            if (featureType == typeof(IAspNetCoreFeature))
            {
                if (feature == null) feature = AspNetCoreFeature.FromHttpContext(Context);
                return feature;
            }
            return BaseCollection?.Get(featureType);
        }

        /// <inheritdoc />
        public void Set(Type featureType, object instance)
        {
            throw new NotSupportedException();
        }
    }
}
