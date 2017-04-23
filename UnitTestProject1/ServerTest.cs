using System;
using System.Diagnostics;
using System.IO;
using JsonRpc.Standard;
using JsonRpc.Standard.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class ServerTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            var request = "{\"jsonrpc\": \"2.0\",\"id\": 1,\"method\": \"sum\",\"params\": {\"x\":100, \"y\":-200}}";
            using (var reader = new StringReader(request))
            using (var writer = new StringWriter())
            {
                var host = Utility.CreateJsonRpcServiceHost(new ByLineTextMessageReader(reader),
                    new ByLineTextMessageWriter(writer));
                host.RunAsync().Wait();
                Trace.WriteLine(writer.ToString());
            }
        }
    }
}
