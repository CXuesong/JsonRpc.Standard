namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents a JSON RPC message reader.
    /// </summary>
    public abstract class MessageReader
    {
        /// <summary>
        /// Reads the next message.
        /// </summary>
        /// <returns>
        /// The next JSON RPC message, or <c>null</c> if no more messages exist.
        /// </returns>
        /// <remarks>This method might block the caller thread until the next message is available.</remarks>
        public abstract Message Read();
    }
}
