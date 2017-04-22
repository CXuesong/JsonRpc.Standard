using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Server;

namespace UnitTestProject1
{
    static class Utility
    {
        private static readonly Lazy<IRpcMethodResolver> rpcMethodResolver = new Lazy<IRpcMethodResolver>(() =>
        {
            var r = new RpcMethodResolver();
            r.Register(typeof(Utility).Assembly);
            return r;
        });

        public static IRpcMethodResolver GetRpcMethodResolver()
        {
            return rpcMethodResolver.Value;
        }

        public static IJsonRpcServiceHost CreateJsonRpcServiceHost(MessageReader reader, MessageWriter writer)
        {
            return new JsonRpcServiceHost(reader, writer, GetRpcMethodResolver(), JsonRpcServiceHostOptions.None);
        }
    }
}
