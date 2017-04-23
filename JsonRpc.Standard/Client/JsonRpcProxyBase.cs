using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard.Contracts;
using Newtonsoft.Json.Linq;

namespace JsonRpc.Standard.Client
{
    internal class JsonRpcProxyBase
    {

        public JsonRpcProxyBase(JsonRpcClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            Client = client;
        }

        public JsonRpcClient Client { get; }

        protected TResult Send<TResult>(JsonRpcMethod method, IList<object> paramValues)
        {
            return SendAsync<TResult>(method, paramValues).Result;
        }

        protected async Task<TResult> SendAsync<TResult>(JsonRpcMethod method, IList<object> paramValues)
        {
            var response = await SendInternalAsync(method, paramValues);
            if (method.ReturnParameter.ParameterType != typeof(void))
                return response.Result.ToObject<TResult>(method.ReturnParameter.Serializer);
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
                    var value = JToken.FromObject(paramValues[i], method.Parameters[i].Serializer);
                    jargs.Add(method.Parameters[i].ParameterName, value);
                }
            }
            // Send the request
            if (method.IsNotification)
            {
                await Client.SendAsync(new NotificationMessage(method.MethodName, jargs), ct);
                return null;
            }
            else
            {
                return await Client.SendAsync(new RequestMessage(Client.NextRequestId(), method.MethodName), ct);
            }
        }

    }

    internal interface IContractTest
    {
        object M1(int a, string b, object c, int? d = 10);
    }

    internal class JsonRpcProxyTest : JsonRpcProxyBase, IContractTest
    {
        /// <inheritdoc />
        public JsonRpcProxyTest(JsonRpcClient client) : base(client)
        {
        }

        /// <inheritdoc />
        object IContractTest.M1(int a, string b, object c, int? d)
        {
            return Send<object>(null, new object[] {a, b, c, d});
        }
    }
}

