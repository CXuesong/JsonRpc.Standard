using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Represents a message writer that writes the message line-by-line to <see cref="TextWriter"/>.
    /// </summary>
    public class ByLineTextMessageWriter : MessageWriter
    {
        private readonly SemaphoreSlim writerSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initialize a line-by-line message writer to <see cref="TextWriter" />.
        /// </summary>
        /// <param name="writer">The underlying text writer.</param>
        public ByLineTextMessageWriter(TextWriter writer) : this(writer, null)
        {
        }

        /// <summary>
        /// Initialize a message writer to <see cref="TextWriter" />.
        /// </summary>
        /// <param name="writer">The underlying text writer.</param>
        /// <param name="delimiter">
        /// The indicator for the end of a message.
        /// If the writer writes a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </param>
        public ByLineTextMessageWriter(TextWriter writer, string delimiter)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            Writer = writer;
            Delimiter = delimiter;
        }

        /// <summary>
        /// The underlying text writer.
        /// </summary>
        public TextWriter Writer { get; }

        /// <summary>
        /// The indicator for the end of a message.
        /// </summary>
        /// <remarks>
        /// If the writer writes a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </remarks>
        public string Delimiter { get; }

        /// <inheritdoc />
        public override async Task WriteAsync(Message message, CancellationToken cancellationToken)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            cancellationToken.ThrowIfCancellationRequested();
            await writerSemaphore.WaitAsync(cancellationToken);
            try
            {
                var content = RpcSerializer.SerializeMessage(message);
                await Writer.WriteLineAsync(content);
                if (Delimiter != null) await Writer.WriteLineAsync();
            }
            finally
            {
                writerSemaphore.Release();
            }
        }
    }
}
