using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
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
            (host, client, hostLifetime, clientLifetime) = Utility.CreateJsonRpcHostClient(output);
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
            Assert.Equal(100, proxy.Add(73, 27));
            Assert.Equal("abcdef", proxy.Add("ab", "cdef"));
            Assert.Equal(new Complex(100, 200), await proxy.MakeComplex(100, 200));
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
