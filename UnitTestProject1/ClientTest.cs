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

        public ClientTest(ITestOutputHelper output) : base(output)
        {
            (host, client, hostLifetime, clientLifetime) = Utility.CreateJsonRpcHostClient(this);
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
            var builder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
            var proxy = builder.CreateProxy<ITestRpcContract>(client);
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
            var responses = await Task.WhenAll(requests.Select(r =>
                client.SendRequestAsync(r.Name, r.Parameters == null ? null : JToken.FromObject(r.Parameters),
                    CancellationToken.None)));
            foreach ((var request, var response)
                in requests.Zip(responses, (req, res) => (Request:req, Response: res)))
            {
                Output.WriteLine(request + "");
                Assert.Null(response.Result);
                Assert.NotNull(response.Error);
                Assert.Equal(request.ExpectedErrorCode, (JsonRpcErrorCode) response.Error.Code);
            }
        }

        [Fact]
        public async Task ProxyExceptionTest()
        {
            var builder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
            var proxy = builder.CreateProxy<ITestRpcExceptionContract>(client);
            var ex = Assert.Throws<JsonRpcRemoteException>(() => proxy.ThrowException());
            Output.WriteLine(await Assert.ThrowsAsync<JsonRpcRemoteException>(() => proxy.ThrowExceptionAsync()) + "");
        }
    }
}
