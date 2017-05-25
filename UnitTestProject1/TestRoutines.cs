using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
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
                Assert.Equal(JsonRpcErrorCode.MethodNotFound, (JsonRpcErrorCode)ex.Error.Code);
            }
            {
                var ex = await Assert.ThrowsAsync<JsonRpcContractException>(stub.ContractViolatingMethodAsync);
            }
        }
    }
}
