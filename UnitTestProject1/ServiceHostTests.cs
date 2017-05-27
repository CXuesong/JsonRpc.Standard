using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class ServiceHostTests : UnitTestBase
    {
        /// <inheritdoc />
        public ServiceHostTests(ITestOutputHelper output) : base(output)
        {
        }


        [Fact]
        public async Task BasicServiceHostTest()
        {
            var host = Utility.CreateJsonRpcServiceHost(this);
            var response = await host.InvokeAsync(
                new RequestMessage(123, "add", JToken.FromObject(new { x = 20, y = 35 })),
                null,
                CancellationToken.None);
            Assert.NotNull(response);
            Assert.Equal(123, response.Id);
            Assert.Null(response.Error);
            Assert.Equal(55, (int)response.Result);
            response = await host.InvokeAsync(
                new RequestMessage("TEST", "add", JToken.FromObject(new { a = "abc", b = "def" })),
                null,
                CancellationToken.None);
            Assert.NotNull(response);
            Assert.Equal("TEST", response.Id);
            Assert.Null(response.Error);
            Assert.Equal("abcdef", (string)response.Result);
            response = await host.InvokeAsync(
                new RequestMessage(456, "throwException"),
                null,
                CancellationToken.None);
            Assert.NotNull(response);
            Assert.Equal(456, response.Id);
            Assert.Null(response.Result);
            Assert.NotNull(response.Error);
            Assert.Equal(JsonRpcErrorCode.UnhandledClrException, (JsonRpcErrorCode) response.Error.Code);
        }

    }
}
