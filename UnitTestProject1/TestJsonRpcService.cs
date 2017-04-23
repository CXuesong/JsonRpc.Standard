using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;

namespace UnitTestProject1
{

    public interface ITestRpcContract
    {
        [JsonRpcMethod]
        int Sum(int x, int y);

        [JsonRpcMethod]
        string Concat(string a, string b);

        [JsonRpcMethod]
        Task<Complex> MakeComplex(double real, double imaginary);

    }

    public class TestJsonRpcService : JsonRpcService
    {
        [JsonRpcMethod]
        public async Task<int> Sum(int x, int y, CancellationToken ct)
        {
            await Task.Delay(500, ct);
            return x + y;
        }

        [JsonRpcMethod]
        public string Concat(string a, string b)
        {
            return a + b;
        }

        [JsonRpcMethod]
        public Complex MakeComplex(double real, double imaginary)
        {
            return new Complex(real, imaginary);
        }
    }
}
