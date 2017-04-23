using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard.Server;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class ServerExtensions
    {
        /// <summary>
        /// Asynchronously starts the JSON RPC service host.
        /// </summary>
        public static Task RunAsync(this IJsonRpcServiceHost host)
        {
            return host.RunAsync(CancellationToken.None);
        }
    }
}
