using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
    [Browsable(false)]
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

        /// <summary>
        /// Infrastructure. Sends the request and wait for the response.
        /// </summary>
        protected TResult Send<TResult>(int methodIndex, IList paramValues)
        {
            return SendAsync<TResult>(methodIndex, paramValues).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Infrastructure. Asynchronously sends the request and wait for the response.
        /// </summary>
        /// <typeparam name="TResult">Response type.</typeparam>
        /// <param name="methodIndex">The JSON RPC method index in <see cref="MethodTable"/>.</param>
        /// <param name="paramValues">Parameters, in the order of expected parameter order.</param>
        /// <exception cref="JsonRpcRemoteException">An error has occurred on the remote-side.</exception>
        /// <exception cref="JsonRpcContractException">An error has occurred when generating the request or parsing the response.</exception>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        /// <returns>The response.</returns>
        protected async Task<TResult> SendAsync<TResult>(int methodIndex, IList paramValues)
        {
            var method = MethodTable[methodIndex];
            MarshaledRequest marshaled;
            try
            {
                marshaled = method.Marshal(paramValues);
            }
            catch (Exception ex)
            {
                throw new JsonRpcContractException("An exception occured while marshalling the request. " + ex.Message,
                    ex);
            }
            marshaled.CancellationToken.ThrowIfCancellationRequested();
            // Send the request
            if (!method.IsNotification) marshaled.Message.Id = Client.NextRequestId();
            var response = await Client.SendAsync(marshaled.Message, marshaled.CancellationToken).ConfigureAwait(false);
            // For notification, we do not have a response.
            if (response != null)
            {
                if (response.Error != null)
                {
                    throw new JsonRpcRemoteException(response.Error);
                }
                if (method.ReturnParameter.ParameterType != typeof(void))
                {
                    // VSCode will return void for null in `window/showMessageRequest`.
                    // I mean, https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#window_showMessageRequest
                    // So just don't be picky…
                    //if (response.Result == null)
                    //    throw new JsonRpcContractException(
                    //        $"Expect \"{method.ReturnParameter.ParameterType}\" result, got void.",
                    //        message);
                    try
                    {
                        return (TResult) method.ReturnParameter.Converter.JsonToValue(response.Result, typeof(TResult));
                    }
                    catch (Exception ex)
                    {
                        throw new JsonRpcContractException(
                            "An exception occured while unmarshalling the response. " + ex.Message,
                            marshaled.Message, ex);
                    }
                }
            }
            return default(TResult);
        }
    }

#if DEBUG
    //… So that I can see some IL

    internal interface IContractTest
    {
        object M1(int a, string b, object c, HashSet<int>.Enumerator d);

        int P1 { get; set; }
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

        /// <inheritdoc />
        public int P1 { get; set; }

        void XXX()
        {
            Send<object>(1, null);
        }
    }
#endif
}

