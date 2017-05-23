using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;

namespace JsonRpc.Dataflow
{
    /// <summary>
    /// Represents a message reader that parses the message line-by-line from <see cref="TextReader"/>.
    /// </summary>
    public class ByLineTextMessageSourceBlock : BufferedMessageSourceBlock
    {
        /// <summary>
        /// Initialize a line-by-line message reader from <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The underlying text reader.</param>
        public ByLineTextMessageSourceBlock(TextReader reader) : this(reader, null)
        {
        }

        /// <summary>
        /// Initialize a message reader from <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The underlying text reader.</param>
        /// <param name="delimiter">
        /// The indicator for the end of a message.
        /// If the reader reads a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </param>
        public ByLineTextMessageSourceBlock(TextReader reader, string delimiter)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            Reader = reader;
            Delimiter = delimiter;
        }

        /// <summary>
        /// The underlying text reader.
        /// </summary>
        public TextReader Reader { get; }

        /// <summary>
        /// The indicator for the end of a message.
        /// </summary>
        /// <remarks>
        /// If the reader reads a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </remarks>
        public string Delimiter { get; }

        /// <inheritdoc />
        protected override async Task<Message> ReadMessageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (Delimiter == null)
                {
                    string line;
                    do
                    {
                        line = await Reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) return null;
                    } while (string.IsNullOrWhiteSpace(line));
                    return Message.LoadJson(line);
                }
                else
                {
                    string line;
                    var builder = new StringBuilder();
                    do
                    {
                        if (builder.Length == 0) cancellationToken.ThrowIfCancellationRequested();
                        line = await Reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line)) builder.AppendLine(line);
                    } while (line != null);
                    if (builder.Length == 0) return null;
                    return Message.LoadJson(builder.ToString());
                }
            }
            catch (ObjectDisposedException)
            {
                // Throws OperationCanceledException if the cancellation has already been requested.
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
        }
    }
}
