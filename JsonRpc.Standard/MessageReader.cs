using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents a JSON RPC message reader.
    /// </summary>
    public abstract class MessageReader
    {
        /// <summary>
        /// Asynchronously reads the next message.
        /// </summary>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>
        /// The next JSON RPC message, or <c>null</c> if no more messages exist.
        /// </returns>
        public abstract Task<Message> ReadAsync(CancellationToken cancellationToken);
    }
}
