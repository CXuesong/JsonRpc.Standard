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

namespace UnitTestProject1
{
    public class ClientTest
    {
        [Fact]
        public async Task ProxyTest()
        {
            (var host, var client, var hostLifetime, var clientLifetime) = Utility.CreateJsonRpcHostClient();
            using (hostLifetime)
            using (clientLifetime)
            {
                var builder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
                var proxy = builder.CreateProxy<ITestRpcContract>(client);
                Assert.Equal(100, proxy.Add(73, 27));
                Assert.Equal("abcdef", proxy.Add("ab", "cdef"));
                Assert.Equal(new Complex(100, 200), await proxy.MakeComplex(100, 200));
            }
        }
    }
}
