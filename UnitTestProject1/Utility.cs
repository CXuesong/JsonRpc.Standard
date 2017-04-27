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
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    static class Utility
    {
        public static readonly JsonRpcContractResolver DefaultContractResolver = new JsonRpcContractResolver
        {
            NamingStrategy = JsonRpcNamingStrategies.CamelCase,
            ParameterValueConverter = JsonValueConverters.CamelCase
        };

        public static IJsonRpcServiceHost CreateJsonRpcHost(ITestOutputHelper output)
        {
            var builder = new ServiceHostBuilder();
            builder.Register(typeof(Utility).Assembly);
            builder.ContractResolver = DefaultContractResolver;
            if (output != null)
            {
                builder.Intercept(async (context, next) =>
                {
                    output.WriteLine("> {0}", context.Request);
                    await next();
                    output.WriteLine("< {0}", context.Response);
                });
            }
            return builder.Build();
        }

        public static
            (IJsonRpcServiceHost Host, JsonRpcClient Client, IDisposable HostLifetime, IDisposable ClientLifetime)
            CreateJsonRpcHostClient(ITestOutputHelper output)
        {
            var server = CreateJsonRpcHost(output);
            var client = new JsonRpcClient();
            var serverBuffer = new BufferBlock<Message>();
            var clientBuffer = new BufferBlock<Message>();
            var lifetime1 = server.Attach(clientBuffer, serverBuffer);
            var lifetime2 = client.Attach(serverBuffer, clientBuffer);
            return (server, client, lifetime1, lifetime2);
        }
    }
}
