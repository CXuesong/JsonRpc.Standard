using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class ServerTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            var request = "{\"jsonrpc\": \"2.0\",\"id\": 1,\"method\": \"sum\",\"params\": {\"a\":100, \"b\":-200}}";

        }
    }
}
