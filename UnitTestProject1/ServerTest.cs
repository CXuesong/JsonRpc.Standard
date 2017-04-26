using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Dataflow;
using JsonRpc.Standard.Server;
using Xunit;

namespace UnitTestProject1
{
    public class ServerTest
    {
        [Fact]
        public async Task TestMethod1()
        {
            var request = "{\"jsonrpc\": \"2.0\",\"id\": 1,\"method\": \"sum\",\"params\": {\"x\":100, \"y\":-200}}";
            using (var reader = new StringReader(request))
            using (var writer = new StringWriter())
            {
                var host = Utility.CreateJsonRpcHost();
                var mreader = new ByLineTextMessageSourceBlock(reader);
                using (host.Attach(mreader, new ByLineTextMessageTargetBlock(writer)))
                {
                    await mreader.Completion;
                }
                Trace.WriteLine(writer.ToString());
            }
        }
    }
}
