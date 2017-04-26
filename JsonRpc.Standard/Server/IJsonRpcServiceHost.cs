using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Used to control the lifecycle of a JSON RPC service host.
    /// </summary>
    public interface IJsonRpcServiceHost
    {
        /// <summary>
        /// The factory that creates the JSON RPC service instances to handle the requests.
        /// </summary>
        IServiceFactory ServiceFactory { get; }

        /// <summary>
        /// Attaches the host to the specific source block and target block.
        /// </summary>
        /// <param name="source">The source block used to retrieve the requests.</param>
        /// <param name="target">The target block used to emit the responses.</param>
        /// <returns>A <see cref="IDisposable"/> used to disconnect the source and target blocks.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="target"/> is <c>null</c>.</exception>
        IDisposable Attach(ISourceBlock<Message> source, ITargetBlock<ResponseMessage> target);
    }
}