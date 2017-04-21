using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Provides the context per <see cref="JsonRpcService"/>.
    /// </summary>
    public class ServiceContext
    {
        private static readonly JsonSerializer defaultSerializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private JsonSerializer _JsonSerializer = defaultSerializer;

        /// <summary>
        /// The JSON serializer used to parse the request content.
        /// </summary>
        public JsonSerializer JsonSerializer
        {
            get { return _JsonSerializer; }
            set { _JsonSerializer = value ?? defaultSerializer; }
        }
    }
}
