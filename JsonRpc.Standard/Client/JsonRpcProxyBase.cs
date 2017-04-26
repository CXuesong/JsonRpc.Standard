using System;
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

        protected TResult Send<TResult>(int methodIndex, IList<object> paramValues)
        {
            return SendAsync<TResult>(methodIndex, paramValues).GetAwaiter().GetResult();
        }

        protected async Task<TResult> SendAsync<TResult>(int methodIndex, IList<object> paramValues)
        {
            var method = MethodTable[methodIndex];
            var response = await SendInternalAsync(method, paramValues).ConfigureAwait(false);
            if (response.Error != null)
            {
                if (response.Error.Code == (int) JsonRpcErrorCode.UnhandledClrException)
                {
                    var error = response.Error.Data.ToObject<UnhandledClrExceptionData>(RpcSerializer.Serializer);
                    throw new TargetInvocationException(error.ToException());
                }
                else
                {
                    throw new TargetInvocationException(new JsonRpcException(response.Error.Code,
                        response.Error.Message, response.Error.Data));
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

        protected async Task<ResponseMessage> SendInternalAsync(JsonRpcMethod method, IList<object> paramValues)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            CancellationToken ct = CancellationToken.None;
            // Parse parameters
            JObject jargs = null;
            if (method.Parameters.Count > 0)
            {
                if (paramValues == null) throw new ArgumentNullException(nameof(paramValues));
                if (method.Parameters.Count != paramValues.Count)
                    throw new ArgumentException(
                        $"Incorrect arguments count. Expect: {method.Parameters.Count}, actual: {paramValues.Count}.");
                jargs = new JObject();
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    if (method.Parameters[i].IsOptional && paramValues[i] == Type.Missing)
                        continue;
                    if (method.Parameters[i].ParameterType == typeof(CancellationToken))
                    {
                        ct = (CancellationToken) paramValues[i];
                        continue;
                    }
                    var value = method.Parameters[i].Converter.ValueToJson(paramValues[i]);
                    jargs.Add(method.Parameters[i].ParameterName, value);
                }
            }
            // Send the request
            if (method.IsNotification)
            {
                await Client.SendAsync(new NotificationMessage(method.MethodName, jargs), ct).ConfigureAwait(false);
                return null;
            }
            else
            {
                return await Client.SendAsync(new RequestMessage(Client.NextRequestId(), method.MethodName, jargs), ct)
                    .ConfigureAwait(false);
            }
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

