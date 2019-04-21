using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Represents a message reader that parses the message line-by-line from <see cref="TextReader"/>.
    /// </summary>
    public class ByLineTextMessageReader : QueuedMessageReader
    {

        private readonly SemaphoreSlim readerSemaphore = new SemaphoreSlim(1, 1);
        private Stream underlyingStream;

        /// <summary>
        /// Initialize a line-by-line message reader from <see cref="TextReader" />.
        /// </summary>
        /// <param name="reader">The underlying text reader.</param>
        public ByLineTextMessageReader(TextReader reader) : this(reader, null)
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
        public ByLineTextMessageReader(TextReader reader, string delimiter)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            Reader = reader;
            Delimiter = delimiter;
        }

        /// <summary>
        /// Initialize a line-by-line message reader from <see cref="Stream" />.
        /// </summary>
        /// <param name="stream">The underlying stream. A <see cref="TextReader"/> will be created based on it.</param>
        public ByLineTextMessageReader(Stream stream) : this(stream, null)
        {
        }

        /// <summary>
        /// Initialize a message reader from <see cref="Stream" />.
        /// </summary>
        /// <param name="stream">The underlying stream. A <see cref="TextReader"/> with UTF-8 encoding will be created based on it.</param>
        /// <param name="delimiter">
        /// The indicator for the end of a message.
        /// If the reader reads a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </param>
        public ByLineTextMessageReader(Stream stream, string delimiter)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            underlyingStream = stream;
            Reader = new StreamReader(stream, Utility.UTF8NoBom, false, 1024, true);
            Delimiter = delimiter;
        }

        /// <summary>
        /// The underlying text reader.
        /// </summary>
        public TextReader Reader { get; private set; }

        /// <summary>
        /// The indicator for the end of a message.
        /// </summary>
        /// <remarks>
        /// If the reader reads a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </remarks>
        public string Delimiter { get; }

        /// <summary>
        /// Whether to leave <see cref="Reader"/> or <see cref="Stream"/> (if this instance is initialized with a Stream)
        /// open when disposing this instance.
        /// </summary>
        public bool LeaveReaderOpen { get; set; }

        /// <inheritdoc />
        protected override async Task<Message> ReadDirectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await readerSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string line;
                if (Delimiter == null)
                {
                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        line = await Reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) return null;
                    } while (string.IsNullOrWhiteSpace(line));
                }
                else
                {
                    var builder = new StringBuilder();
                    do
                    {
                        if (builder.Length == 0) cancellationToken.ThrowIfCancellationRequested();
                        line = await Reader.ReadLineAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(line)) builder.AppendLine(line);
                    } while (line != null);
                    if (builder.Length == 0) return null;
                }
                try
                {
                    return Message.LoadJson(line);
                }
                catch (Exception ex)
                {
                    throw new MessageReaderException("Cannot parse the message body into a valid JSON RPC message.", ex);
                }
            }
            catch (ObjectDisposedException)
            {
                // Throws OperationCanceledException if the cancellation has already been requested.
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            finally
            {
                // Note: when DisposalToken cancels, this part of code might execute concurrently with Dispose().
                // We might receive ObjectDisposedException if we are taking longer time than expected (>1s) to reach here.
                readerSemaphore.Release();
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Reader == null) return;
            // Give ReadDirectAsync some time to complete the operation gracefully.
            // Hold the semaphore before disposal.
            var readerSemaphoreVacant = readerSemaphore.Wait(1000);
            // If we are still in StreamReader.ReadAsync, Disposing StreamReader may cause Exception.
            // Inspecting full call stack at this point may be helpful.
            Debug.Assert(readerSemaphoreVacant, "Attempt to dispose ByLineTextMessageReader when ReadDirectAsync hasn't finished yet.");
            if (underlyingStream != null)
            {
                Utility.TryDispose(Reader, this);
            }
            if (!LeaveReaderOpen)
            {
                if (underlyingStream != null)
                {
                    Utility.TryDispose(underlyingStream, this);
                }
                else
                {
                    Utility.TryDispose(Reader, this);
                }
            }
            readerSemaphore.Dispose();
            underlyingStream = null;
            Reader = null;
        }
    }
}
