using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Dataflow;
using JsonRpc.Standard.Server;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class ServerTest : UnitTestBase
    {
        public ServerTest(ITestOutputHelper output) : base(output)
        {
            
        }

        [Fact]
        public async Task TestMethod1()
        {
            var request = "{\"jsonrpc\": \"2.0\",\"id\": 1,\"method\": \"sum\",\"params\": {\"x\":100, \"y\":-200}}";
            using (var reader = new StringReader(request))
            using (var writer = new StringWriter())
            {
                var host = Utility.CreateJsonRpcHost(Output);
                var source = new ByLineTextMessageSourceBlock(reader);
                var target = new ByLineTextMessageTargetBlock(writer);
                using (host.Attach(source, target))
                {
                    await target.Completion;
                }
                var result = writer.ToString();
                Assert.Equal("{\"id\":1,\"result\":-100,\"jsonrpc\":\"2.0\"}", result.Trim());
            }
        }
    }
}
