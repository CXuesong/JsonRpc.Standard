using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.DynamicProxy.Client;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnitTestProject1.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class ClientTests : UnitTestBase
    {
        private readonly JsonRpcDirectHandler handler;
        private readonly IJsonRpcServiceHost serviceHost;
        private readonly JsonRpcProxyBuilder proxyBuilder;
        private readonly JsonRpcClient client;

        public ClientTests(ITestOutputHelper output) : base(output)
        {
            serviceHost = Utility.CreateJsonRpcServiceHost(this);
            handler = new JsonRpcDirectHandler(serviceHost);
            client = new JsonRpcClient(handler);
            client.RequestCancelling += (_, e) =>
            {
                ((JsonRpcClient) _).SendNotificationAsync("cancelRequest", JToken.FromObject(new {id = e.RequestId}),
                    CancellationToken.None);
            };
            proxyBuilder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
        }

        [Fact]
        public async Task ProxyTest()
        {
            var proxy = proxyBuilder.CreateProxy<ITestRpcContract>(client);
            await TestRoutines.TestStubAsync(proxy);
        }

        [Fact]
        public async Task AbstractClassProxyTest()
        {
            var proxy = proxyBuilder.CreateProxy<TestRpcAbstractClassContract>(client);
            Assert.Equal(3, await proxy.OnePlusTwo());
        }

        [Fact]
        public async Task ProxyExceptionTest()
        {
            var proxy = proxyBuilder.CreateProxy<ITestRpcExceptionContract>(client);
            await TestRoutines.TestStubAsync(proxy);
        }

        // You should see something linke this in the output
        // {"method":"cancelRequest","params":{"id":"46361581#1"},"jsonrpc":"2.0"}
        [Fact]
        public async Task ProxyCancellationTest()
        {
            var proxy = proxyBuilder.CreateProxy<ITestRpcContract>(client);
            using (var cts = new CancellationTokenSource(500))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => proxy.DelayAsync(TimeSpan.FromMilliseconds(1000), cts.Token));
            }
            await Task.Delay(500);
        }

        [Fact]
        public async Task MethodResolutionTest()
        {
            var requests = new (string Name, object Parameters, JsonRpcErrorCode ExpectedErrorCode)[]
            {
                ("non-existent", null, JsonRpcErrorCode.MethodNotFound),
                ("add", 123, JsonRpcErrorCode.InvalidRequest),
                ("add", new {x = "true", y = 100}, JsonRpcErrorCode.InvalidParams),
                ("add", new {a = 100, b = 100}, JsonRpcErrorCode.InvalidParams)
            };
            foreach (var request in requests)
            {
                Output.WriteLine(request + "");
                var response = await client.SendRequestAsync(request.Name,
                    request.Parameters == null ? null : JToken.FromObject(request.Parameters),
                    CancellationToken.None);
                Assert.Null(response.Result);
                Assert.NotNull(response.Error);
                Assert.Equal(request.ExpectedErrorCode, (JsonRpcErrorCode) response.Error.Code);
            }
        }

    }
}
