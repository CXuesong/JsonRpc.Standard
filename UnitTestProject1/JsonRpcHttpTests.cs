using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Http;
using JsonRpc.Standard.Client;
using UnitTestProject1.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class JsonRpcHttpTests : UnitTestBase
    {

        /// <inheritdoc />
        public JsonRpcHttpTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ClientInteropTest()
        {
            var host = Utility.CreateJsonRpcServiceHost(this);
            var handler =
                new HttpRpcClientHandler(new JsonRpcHttpMessageDirectHandler(host))
                {
                    EndpointUrl = "http://localhost:1234/fakepath"
                };
            var client = new JsonRpcClient(handler);
            var builder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
            var stub1 = builder.CreateProxy<ITestRpcContract>(client);
            var stub2 = builder.CreateProxy<ITestRpcExceptionContract>(client);
            await TestRoutines.TestStubAsync(stub1);
            await TestRoutines.TestStubAsync(stub2);
        }
    }
}

