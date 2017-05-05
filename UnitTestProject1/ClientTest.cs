using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class ClientTest : UnitTestBase
    {
        private readonly IJsonRpcServiceHost host;
        private readonly JsonRpcClient client;
        private readonly IDisposable hostLifetime, clientLifetime;
        private readonly JsonRpcProxyBuilder proxyBuilder;

        public ClientTest(ITestOutputHelper output) : base(output)
        {
            (host, client, hostLifetime, clientLifetime) = Utility.CreateJsonRpcHostClient(this);
            proxyBuilder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            hostLifetime.Dispose();
            clientLifetime.Dispose();
        }

        [Fact]
        public async Task ProxyTest()
        {
            var proxy = proxyBuilder.CreateProxy<ITestRpcContract>(client);
            proxy.Delay();
            proxy.Delay(TimeSpan.FromMilliseconds(100));
            Assert.Equal(1, proxy.One());
            Assert.Equal(1, proxy.One(false));
            Assert.Equal(-1, proxy.One(true));
            Assert.Equal(2, proxy.Two());
            Assert.Equal(2, proxy.Two(false));
            Assert.Equal(-2, proxy.Two(true));
            Assert.Equal(100, proxy.Add(73, 27));
            Assert.Equal("abcdef", proxy.Add("ab", "cdef"));
            Assert.Equal(new Complex(100, 200), await proxy.MakeComplex(100, 200));
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

        [Fact]
        public async Task ProxyExceptionTest()
        {
            var proxy = proxyBuilder.CreateProxy<ITestRpcExceptionContract>(client);
            var ex = Assert.Throws<JsonRpcRemoteException>(() => proxy.ThrowException());
            Output.WriteLine(await Assert.ThrowsAsync<JsonRpcRemoteException>(() => proxy.ThrowExceptionAsync()) + "");
            await Assert.ThrowsAsync<JsonRpcContractException>(proxy.Delay);
        }
    }
}
