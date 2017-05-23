using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;

namespace UnitTestProject1
{

    public interface ITestRpcContract
    {
        [JsonRpcMethod]
        int One();

        [JsonRpcMethod]
        int One(bool negative);

        [JsonRpcMethod]
        int Two();

        [JsonRpcMethod]
        int Two(bool negative);

        [JsonRpcMethod]
        int Add(int x, int y);

        [JsonRpcMethod]
        string Add(string a, string b);

        [JsonRpcMethod]
        Task<Complex> MakeComplex(double real, double imaginary);

        [JsonRpcMethod("delay")]
        Task DelayAsync(TimeSpan duration, CancellationToken ct);

        [JsonRpcMethod]
        void Delay(TimeSpan duration);

        [JsonRpcMethod]
        void Delay();

        [JsonRpcMethod(IsNotification = true)]
        void CancelRequest(MessageId id);
    }

    public interface ITestRpcExceptionContract
    {

        [JsonRpcMethod]
        void ThrowException();

        [JsonRpcMethod("throwException")]
        Task ThrowExceptionAsync();

        [JsonRpcMethod]
        Task<int> Delay();

    }

    public class TestJsonRpcService : JsonRpcService
    {
        [JsonRpcMethod]
        public int One()
        {
            return 1;
        }

        [JsonRpcMethod]
        public int One([JsonRpcParameter("negative")] bool neg)
        {
            return neg ? -1 : 1;
        }

        [JsonRpcMethod]
        public int Two(bool negative = false)
        {
            return negative ? -2 : 2;
        }

        [JsonRpcMethod]
        public Task<int> Sum(int x, int y, CancellationToken ct)
        {
            // For backward compatibility
            return Add(x, y, ct);
        }

        [JsonRpcMethod]
        public async Task<int> Add(int x, int y, CancellationToken ct)
        {
            await Task.Delay(500, ct);
            return x + y;
        }

        [JsonRpcMethod]
        public string Add(string a, string b)
        {
            return a + b;
        }

        [JsonRpcMethod]
        public Complex MakeComplex(double real, double imaginary)
        {
            return new Complex(real, imaginary);
        }

        [JsonRpcMethod]
        public Task ThrowException()
        {
            throw new InvalidOperationException("The operation is invalid.", new InvalidTimeZoneException());
        }

        [JsonRpcMethod]
        public Task Delay(TimeSpan duration, CancellationToken ct)
        {
            return Task.Delay(duration, ct);
        }

        [JsonRpcMethod]
        public void Delay(CancellationToken ct)
        {
            Task.Delay(100, ct).Wait(ct);
        }

        [JsonRpcMethod(IsNotification = true)]
        public void CancelRequest(MessageId id)
        {
            RequestContext.Features.Get<IRequestCancellationFeature>().TryCancel(id);
        }
    }
}
