using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class ClientTest
    {
        [TestMethod]
        public void ProxyTest()
        {
            (var host, var reader, var writer) = Utility.CreateJsonRpcServiceHost();
            host.RunAsync();
            var client = new JsonRpcClient(reader, writer);
            var builder = new JsonRpcProxyBuilder();
            var proxy = builder.CreateProxy<ITestRpcContract>(client);
            Assert.AreEqual(100, proxy.Sum(73, 27));
            Assert.AreEqual("abcdef", proxy.Concat("ab", "cdef"));
            Assert.AreEqual(new Complex(100, 200), proxy.MakeComplex(100, 200).Result);
            host.Stop();
        }
    }
}
