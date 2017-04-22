using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonRpc.Standard.Server;

namespace UnitTestProject1
{
    public class TestJsonRpcService : JsonRpcService
    {
        [JsonRpcMethod]
        public int Sum(int x, int y)
        {
            return x + y;
        }
    }
}
