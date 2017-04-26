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
using Xunit;

namespace UnitTestProject1
{
    static class Utility
    {
        public static readonly JsonRpcContractResolver DefaultContractResolver = new JsonRpcContractResolver
        {
            NamingStrategy = JsonRpcNamingStrategies.CamelCase,
            ParameterValueConverter = JsonValueConverters.CamelCase
        };

        public static IJsonRpcServiceHost CreateJsonRpcHost()
        {
            var builder = new ServiceHostBuilder();
            builder.Register(typeof(Utility).Assembly);
            builder.ContractResolver = DefaultContractResolver;
            return builder.Build();
        }

        public static
            (IJsonRpcServiceHost Host, JsonRpcClient Client, IDisposable HostLifetime, IDisposable ClientLifetime)
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
