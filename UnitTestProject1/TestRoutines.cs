using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Client;
using JsonRpc.Messages;
using UnitTestProject1.Helpers;
using Xunit;

namespace UnitTestProject1
{
    public static class TestRoutines
    {

        public static async Task TestStubAsync(ITestRpcContract stub)
        {
            Assert.Equal(1, stub.One());
            Assert.Equal(1, stub.One(false));
            Assert.Equal(-1, stub.One(true));
            Assert.Equal(2, stub.Two());
            Assert.Equal(2, stub.Two(false));
            Assert.Equal(-2, stub.Two(true));
            Assert.Equal(100, stub.Add(73, 27));
            Assert.Equal("abcdef", stub.Add("ab", "cdef"));
            Assert.Equal(6, stub.AddToSix());
            Assert.Equal(new Complex(100, 200), await stub.MakeComplex(100, 200));
        }

        public static async Task TestStubAsync(ITestRpcExceptionContract stub)
        {
            {
                var ex = Assert.Throws<JsonRpcRemoteException>(() => stub.ThrowException());
                Assert.NotNull(ex.RemoteException);
                Assert.EndsWith("Exception", ex.RemoteException.ExceptionType);
            }
            {
                var ex = await Assert.ThrowsAsync<JsonRpcRemoteException>(stub.ThrowExceptionAsync);
                Assert.NotNull(ex.RemoteException);
                Assert.EndsWith("Exception", ex.RemoteException.ExceptionType);
            }
            {
                var ex = Assert.Throws<JsonRpcRemoteException>(() => stub.ManualResponseError());
                Assert.Null(ex.RemoteException);
                Assert.Equal(123456, ex.Error.Code);
            }
            {
                var ex = Assert.Throws<JsonRpcRemoteException>(() => stub.MissingMethod());
                Assert.Null(ex.RemoteException);
                Assert.Equal(JsonRpcErrorCode.MethodNotFound, (JsonRpcErrorCode) ex.Error.Code);
            }
            {
                var ex = Assert.Throws<JsonRpcRemoteException>(() => stub.MismatchedMethod());
                Assert.Null(ex.RemoteException);
                Assert.Equal(JsonRpcErrorCode.MethodNotFound, (JsonRpcErrorCode) ex.Error.Code);
            }
            {
                var ex = await Assert.ThrowsAsync<JsonRpcContractException>(stub.ContractViolatingMethodAsync);
            }
        }

        public static async Task TestCancellationAsync(ITestRpcCancellationContract stub)
        {
            await stub.Delay(TimeSpan.FromMilliseconds(50));
            Assert.True(await stub.IsLastDelayFinished());
            using (var cts = new CancellationTokenSource())
            {
                // Note: When the request cancellation notification is issued from client,
                //      a TaskCancelledException will be thrown immediately regardless of the service handler's state.
                //      we can check whether the request has really been cancelled by enabling ConsistentResponseSequence.
                // TODO implement ability to wait for "request cancelled" response and put it into some OperationCancelledException-derived class
                //      in JsonRpcClient.
                // 1. Issue the request. 500ms should be enough for us to issue a cancellation.
                var delayTask = stub.Delay(TimeSpan.FromSeconds(500), cts.Token);
                // 2. Issue the cancellation
                cts.Cancel();
                // 3. Ensures the client-side exception
                await Assert.ThrowsAsync<TaskCanceledException>(() => delayTask);
                Assert.False(await stub.IsLastDelayFinished());
                // Ensures that the stub.Delay request has really been cancelled.
                // ReSharper disable once MethodSupportsCancellation
                await Task.Delay(500);
                Assert.False(await stub.IsLastDelayFinished());
                // ReSharper disable once MethodSupportsCancellation
                await Task.Delay(500);
                Assert.False(await stub.IsLastDelayFinished());
            }
        }
    }
}
