using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
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
        public static readonly JsonRpcContractResolver DefaultContractResolver = new JsonRpcContractResolver
        {
            NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
            ParameterValueConverter = new CamelCaseJsonValueConverter()
        };

        public static IJsonRpcServiceHost CreateJsonRpcHost(UnitTestBase owner)
        {
            var builder = new ServiceHostBuilder();
            builder.Register(typeof(Utility).Assembly);
            builder.ContractResolver = DefaultContractResolver;
            if (owner.Output != null)
            {
                builder.Intercept(async (context, next) =>
                {
                    owner.Output.WriteLine("> {0}", context.Request);
                    await next();
                    owner.Output.WriteLine("< {0}", context.Response);
                });
            }
            builder.LoggerFactory = owner.LoggerFactory;
            return builder.Build();
        }

        public static
            (IJsonRpcServiceHost Host, JsonRpcClient Client, IDisposable HostLifetime, IDisposable ClientLifetime)
            CreateJsonRpcHostClient(UnitTestBase owner)
        {
            var server = CreateJsonRpcHost(owner);
            var client = new JsonRpcClient();
            var serverBuffer = new BufferBlock<Message>();
            var clientBuffer = new BufferBlock<Message>();
            var lifetime1 = server.Attach(clientBuffer, serverBuffer);
            var lifetime2 = client.Attach(serverBuffer, clientBuffer);
            return (server, client, lifetime1, lifetime2);
        }
    }
}
