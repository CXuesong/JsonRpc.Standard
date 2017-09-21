using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;

namespace UnitTestProject1.Helpers
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

        [JsonRpcMethod("add")]
        Task<int> AddAsync(int x, int y);

        [JsonRpcMethod]
        string Add(string a, string b);

        [JsonRpcMethod("addMany")]
        int AddToSix();

        [JsonRpcMethod]
        Task<Complex> MakeComplex(double real, double imaginary);

        [JsonRpcMethod("delay")]
        Task DelayAsync(TimeSpan duration, CancellationToken ct);

        [JsonRpcMethod]
        void Delay(TimeSpan duration);

        [JsonRpcMethod]
        void Delay();
    }

    public interface ITestRpcExceptionContract
    {

        [JsonRpcMethod]
        void ThrowException();

        [JsonRpcMethod("throwException")]
        Task ThrowExceptionAsync();

        [JsonRpcMethod("delay")]
        Task<int> ContractViolatingMethodAsync();      // Server returns void, but client requires an int.

        [JsonRpcMethod("sum")]
        int MismatchedMethod();

        [JsonRpcMethod]
        string MissingMethod();

        [JsonRpcMethod]
        void ManualResponseError();
    }

    public interface ITestRpcCancallationContract
    {

        [JsonRpcMethod]
        Task Delay(TimeSpan duration);

        [JsonRpcMethod]
        Task Delay(TimeSpan duration, CancellationToken cancellationToken);

        [JsonRpcMethod]
        Task<bool> IsLastDelayFinished();

        [JsonRpcMethod(IsNotification = true)]
        void CancelRequest(MessageId id);

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
        public int AddMany([JsonRpcParameter(DefaultValue = new[] {1, 2, 3})] int[] values)
        {
            return values.Sum();
        }

        [JsonRpcMethod]
        public int Add(int x, int y, CancellationToken ct)
        {
            return x + y;
        }

        [JsonRpcMethod("add")]
        public async Task<string> AddAsync(string a, string b)
        {
            // do some work asynchronously…
            await Task.Yield();
            // return the result.
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
        public object ManualResponseError()
        {
            return new ResponseError(123456, "Error via ResponseError");
        }

        [JsonRpcMethod]
        public async Task Delay(TimeSpan duration, CancellationToken ct)
        {
            RequestContext.Features.Get<SessionFeature>().IsLastDelayFinished = false;
            await Task.Delay(duration, ct);
            RequestContext.Features.Get<SessionFeature>().IsLastDelayFinished = true;
        }

        [JsonRpcMethod]
        public void Delay(CancellationToken ct)
        {
            RequestContext.Features.Get<SessionFeature>().IsLastDelayFinished = false;
            Task.Delay(100, ct).Wait(ct);
            RequestContext.Features.Get<SessionFeature>().IsLastDelayFinished = true;
        }

        [JsonRpcMethod]
        public bool IsLastDelayFinished()
        {
            return RequestContext.Features.Get<SessionFeature>().IsLastDelayFinished;
        }

        [JsonRpcMethod(IsNotification = true)]
        public void CancelRequest(MessageId id)
        {
            RequestContext.Features.Get<IRequestCancellationFeature>().TryCancel(id);
        }
    }
}
