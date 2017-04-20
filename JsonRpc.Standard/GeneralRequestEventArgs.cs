using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GeneralRequestEventArgs : EventArgs
    {
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; private set; }
    }
}
