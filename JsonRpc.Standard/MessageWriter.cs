using System;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents a JSON RPC message writer.
    /// </summary>
    public abstract class MessageWriter
    {
        /// <summary>
        /// Writes a message.
        /// </summary>
        /// <param name="message">The meesage to write.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
        public abstract void Write(Message message);
    }
}
