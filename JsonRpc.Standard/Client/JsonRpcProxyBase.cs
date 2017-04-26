using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard.Client
{
    /// <summary>
    /// Infrastructure. Base class for client proxy implementation.
    /// </summary>
    public class JsonRpcProxyBase
    {
        protected JsonRpcProxyBase(JsonRpcClient client, IList<JsonRpcMethod> methodTable)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (methodTable == null) throw new ArgumentNullException(nameof(methodTable));
            Client = client;
            MethodTable = methodTable;
        }

        public JsonRpcClient Client { get; }

        protected IList<JsonRpcMethod> MethodTable { get; }

        protected TResult Send<TResult>(int methodIndex, IList paramValues)
        {
            return SendAsync<TResult>(methodIndex, paramValues).GetAwaiter().GetResult();
        }

        protected async Task<TResult> SendAsync<TResult>(int methodIndex, IList paramValues)
        {
            var method = MethodTable[methodIndex];
            var message = method.Marshal(paramValues);
            // Send the request
            if (message is RequestMessage request) request.Id = Client.NextRequestId();
            var response = await Client.SendAsync(message, message.CancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                if (response.Error.Code == (int) JsonRpcErrorCode.UnhandledClrException)
                {
                    var error = response.Error.Data.ToObject<UnhandledClrExceptionData>(RpcSerializer.Serializer);
                    throw new TargetInvocationException(error.ToException());
                }
                else
                {
                    var rpcException = new JsonRpcException(response.Error.Code,
                        response.Error.Message, response.Error.Data);
                    throw new TargetInvocationException(rpcException);
                }
            }
            if (method.ReturnParameter.ParameterType != typeof(void))
            {
                if (response.Result == null)
                    throw new TargetInvocationException(
                        $"Expect \"{method.ReturnParameter.ParameterType}\" result, got void.", null);
                return (TResult) method.ReturnParameter.Converter.JsonToValue(response.Result, typeof(TResult));
            }
            return default(TResult);
        }
    }

    internal interface IContractTest
    {
        object M1(int a, string b, object c, HashSet<int>.Enumerator d);
    }

    internal class JsonRpcProxyTest : JsonRpcProxyBase, IContractTest
    {
        /// <inheritdoc />
        public JsonRpcProxyTest(JsonRpcClient client, JsonRpcMethod[] methodTable) : base(client, methodTable)
        {
        }

        /// <inheritdoc />
        object IContractTest.M1(int a, string b, object c, HashSet<int>.Enumerator d)
        {
            return Send<object>(1, new object[] {a, b, c, d});
        }

        void XXX()
        {
            Send<object>(1, null);
        }
    }
}

