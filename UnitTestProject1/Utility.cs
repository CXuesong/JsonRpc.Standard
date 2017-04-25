using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
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

        public static JsonRpcServiceHost CreateJsonRpcHost()
        {
            return new JsonRpcServiceHost(GetRpcMethodResolver(), JsonRpcServiceHostOptions.None);
        }

        public static
            (JsonRpcServiceHost Host, JsonRpcClient Client, IDisposable HostLifetime, IDisposable ClientLifetime)
            CreateJsonRpcHostClient()
        {
            var server = CreateJsonRpcHost();
            var client = new JsonRpcClient();
            var serverBuffer = new BufferBlock<Message>();
            var clientBuffer = new BufferBlock<Message>();
            var lifetime1 = server.Attach(clientBuffer, serverBuffer);
            var lifetime2 = client.Attach(serverBuffer, clientBuffer);
            return (server, client, lifetime1, lifetime2);
        }
    }
}
