using System;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;

namespace JsonRpc.Server
{
    /// <summary>
    /// Provides methods to dispatch and invoke the specified JSON RPC methods.
    /// </summary>
    public interface IJsonRpcServiceHost
    {
        /// <summary>
        /// Invokes the JSON RPC method.
        /// </summary>
        /// <param name="request">The JSON RPC request.</param>
        /// <param name="features">The features provided along with the request. Use <c>null</c> to indicate default features set.</param>
        /// <param name="cancellationToken">The token used to cancel the request.</param>
        /// <returns>JSON RPC response, or <c>null</c> for JSON RPC notifications.</returns>
        /// <remarks>For cancelled requests, no exception will be thrown, but a response containing <see cref="OperationCanceledException"/> CLR exception will be returned.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
        Task<ResponseMessage> InvokeAsync(RequestMessage request, IFeatureCollection features, CancellationToken cancellationToken);
    }
}