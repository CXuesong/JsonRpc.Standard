using System;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Used to control the lifecycle of a JSON RPC service host.
    /// </summary>
    public interface IJsonRpcServiceHost
    {
        /// <summary>
        /// Asynchronously starts the JSON RPC service host.
        /// </summary>
        /// <param name="cancellationToken">The token used to shut down the service host.</param>
        /// <returns>A task that indicates the host state.</returns>
        /// <exception cref="InvalidOperationException">The service host is already running.</exception>
        Task RunAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Requests to stop the JSON RPC service host.
        /// </summary>
        /// <remarks>This method will do nothing if the service is not started.</remarks>
        void Stop();
    }
}