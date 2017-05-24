using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Represents a JSON RPC message writer.
    /// </summary>
    public abstract class MessageWriter
    {
        /// <summary>
        /// Asynchronously writes a message.
        /// </summary>
        /// <param name="message">The meesage to write.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <remarks>This method should be thread-safe.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
        public abstract Task WriteAsync(Message message, CancellationToken cancellationToken);
    }
}
