using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JsonRpc.Dataflow;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using Newtonsoft.Json.Linq;

namespace UnitTestProject1
{
    static class Utility
    {
        public static readonly JsonRpcContractResolver DefaultContractResolver = new JsonRpcContractResolver
        {
            NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
            ParameterValueConverter = new CamelCaseJsonValueConverter()
        };

        public static IJsonRpcServiceHost CreateJsonRpcServiceHost(UnitTestBase owner)
        {
            var builder = new JsonRpcServiceHostBuilder();
            builder.Register(typeof(Utility).Assembly);
            builder.ContractResolver = DefaultContractResolver;
            if (owner.Output != null)
            {
                builder.Intercept(async (context, next) =>
                {
                    var sw = Stopwatch.StartNew();
                    owner.Output.WriteLine("> {0}", context.Request);
                    try
                    {
                        await next();
                        owner.Output.WriteLine("< {0}", context.Response);
                    }
                    finally
                    {
                        owner.Output.WriteLine("Server: Ellapsed time: {0}", sw.Elapsed);
                    }
                });
            }
            builder.LoggerFactory = owner.LoggerFactory;
            return builder.Build();
        }

        public static DataflowRpcServerHandler CreateJsonRpcHost(UnitTestBase owner)
        {
            var builder = new JsonRpcServiceHostBuilder();
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
            return new DataflowRpcServerHandler(builder.Build(), DataflowRpcServiceHostOptions.ConsistentResponseSequence);
        }

        public static
            (DataflowRpcServerHandler Host, JsonRpcClient Client, IDisposable HostLifetime, IDisposable ClientLifetime)
            CreateJsonRpcHostClient(UnitTestBase owner)
        {
            var server = CreateJsonRpcHost(owner);
            var clientHandler = new DataflowRpcClientHandler();
            var client = new JsonRpcClient(clientHandler);
            client.RequestCancelling += (_, e) =>
            {
                ((JsonRpcClient) _).SendNotificationAsync("cancelRequest", JToken.FromObject(new {id = e.RequestId}),
                    CancellationToken.None);
            };
            var serverBuffer = new BufferBlock<Message>();
            var clientBuffer = new BufferBlock<Message>();
            var lifetime1 = server.Attach(clientBuffer, serverBuffer);
            var lifetime2 = clientHandler.Attach(serverBuffer, clientBuffer);
            return (server, client, lifetime1, lifetime2);
        }

        public static async Task<int> WaitUntilAsync(Func<bool> condition, int maxMilliseconds)
        {
            var start = Environment.TickCount;
            int step;
            if (maxMilliseconds < 100) step = 10;
            else if (maxMilliseconds < 1000) step = 100;
            else step = 1000;
            while (!condition())
            {
                var diff = Environment.TickCount - start;
                if (diff >= maxMilliseconds)
                    return diff;
                await Task.Delay(step);
            }
            return Environment.TickCount - start;
        }
    }
}
