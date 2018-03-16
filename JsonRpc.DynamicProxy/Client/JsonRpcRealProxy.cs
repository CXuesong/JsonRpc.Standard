using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;

namespace JsonRpc.DynamicProxy.Client
{
    /// <summary>
    /// Infrastructure.
    /// Stores method information for implemented proxies in <see cref="JsonRpcProxyBuilder"/>
    /// and provides methods to execute them.
    /// </summary>
    public sealed class JsonRpcRealProxy
    {

        internal readonly JsonRpcClient Client;

        internal readonly IList<JsonRpcMethod> MethodTable;

        internal readonly IJsonRpcRequestMarshaler Marshaler;

        internal JsonRpcRealProxy(JsonRpcClient client, IList<JsonRpcMethod> methodTable, IJsonRpcRequestMarshaler marshaler)
        {
            this.Client = client ?? throw new ArgumentNullException(nameof(client));
            this.MethodTable = methodTable ?? throw new ArgumentNullException(nameof(methodTable));
            this.Marshaler = marshaler ?? throw new ArgumentNullException(nameof(marshaler));
        }

        /// <summary>
        /// Infrastructure. Sends the request and wait for the response.
        /// </summary>
        public TResult Send<TResult>(int methodIndex, IList paramValues)
        {
            return SendAsync<TResult>(methodIndex, paramValues).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Infrastructure. Sends the notification; do not wait for the response.
        /// </summary>
        public void Send(int methodIndex, IList paramValues)
        {
            var forgetit = SendAsync<object>(methodIndex, paramValues);
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
        public async Task<TResult> SendAsync<TResult>(int methodIndex, IList paramValues)
        {
            var method = MethodTable[methodIndex];
            MarshaledRequestParameters marshaled;
            try
            {
                marshaled = Marshaler.MarshalParameters(method.Parameters, paramValues);
            }
            catch (Exception ex)
            {
                throw new JsonRpcContractException("An exception occurred while marshalling the request. " + ex.Message,
                    ex);
            }
            marshaled.CancellationToken.ThrowIfCancellationRequested();
            var request = new RequestMessage(method.MethodName, marshaled.Parameters);
            // Send the request
            if (!method.IsNotification) request.Id = Client.NextRequestId();
            var response = await Client.SendAsync(request, marshaled.CancellationToken).ConfigureAwait(false);
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
                        return (TResult)method.ReturnParameter.Converter.JsonToValue(response.Result, typeof(TResult));
                    }
                    catch (Exception ex)
                    {
                        throw new JsonRpcContractException(
                            "An exception occurred while unmarshalling the response. " + ex.Message,
                            request, ex);
                    }
                }
            }
            return default(TResult);
        }

    }
}
