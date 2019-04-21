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
    /// Represents a message writer that writes the message line-by-line to <see cref="TextWriter"/>.
    /// </summary>
    public class ByLineTextMessageWriter : MessageWriter
    {

        private readonly SemaphoreSlim writerSemaphore = new SemaphoreSlim(1, 1);

        private Stream underlyingStream;

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
        /// Initialize a message writer to <see cref="Stream" />.
        /// </summary>
        /// <param name="stream">The underlying stream. A <see cref="TextWriter"/> with UTF-8 encoding will be created based on it.</param>
        public ByLineTextMessageWriter(Stream stream) : this(stream, null)
        {
        }

        /// <summary>
        /// Initialize a message writer to <see cref="Stream" />.
        /// </summary>
        /// <param name="stream">The underlying stream. A <see cref="TextWriter"/> with UTF-8 encoding will be created based on it.</param>
        /// <param name="delimiter">
        /// The indicator for the end of a message.
        /// If the writer writes a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </param>
        public ByLineTextMessageWriter(Stream stream, string delimiter)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            underlyingStream = stream;
            Writer = new StreamWriter(stream);
            Delimiter = delimiter;
        }


        /// <summary>
        /// The underlying text writer.
        /// </summary>
        public TextWriter Writer { get; private set; }

        /// <summary>
        /// The indicator for the end of a message.
        /// </summary>
        /// <remarks>
        /// If the writer writes a line that is the same as this parameter, the current message is finished.
        /// Use <c>null</c> to indicate that each line, as long as it is not empty,
        /// should be treated a message.
        /// </remarks>
        public string Delimiter { get; }

        /// <summary>
        /// Whether to leave <see cref="Writer"/> or <see cref="Stream"/> (if this instance is initialized with a Stream)
        /// open when disposing this instance.
        /// </summary>
        public bool LeaveWriterOpen { get; set; }

        /// <inheritdoc />
        public override async Task WriteAsync(Message message, CancellationToken cancellationToken)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (DisposalToken.IsCancellationRequested)
                throw new ObjectDisposedException(nameof(ByLineTextMessageWriter));
            cancellationToken.ThrowIfCancellationRequested();
            var linkedToken = DisposalToken;
            CancellationTokenSource linkedTokenSource = null;
            if (cancellationToken.CanBeCanceled)
            {
                linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposalToken);
                linkedToken = linkedTokenSource.Token;
            }
            var content = message.ToString();
            await writerSemaphore.WaitAsync(linkedToken);
            try
            {
                await Writer.WriteLineAsync(content);
                linkedToken.ThrowIfCancellationRequested();
                if (Delimiter != null)
                {
                    await Writer.WriteLineAsync(Delimiter);
                    linkedToken.ThrowIfCancellationRequested();
                }

                await Writer.FlushAsync();
            }
            catch (ObjectDisposedException)
            {
                // Throws OperationCanceledException if the cancellation has already been requested.
                linkedToken.ThrowIfCancellationRequested();
                throw;
            }
            finally
            {
                linkedTokenSource?.Dispose();
                // Note: when DisposalToken cancels, this part of code might execute concurrently with Dispose().
                // We might receive ObjectDisposedException if we are taking longer time than expected (>1s) to reach here.
                writerSemaphore.Release();
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Writer == null) return;
            // Give ReadDirectAsync some time to complete the operation gracefully.
            // Hold the semaphore before disposal.
            var writerSemaphoreVacant = writerSemaphore.Wait(1000);
            // If we are still in StreamWriter.WriteAsync, Disposing StreamWriter may cause Exception.
            // Inspecting full call stack at this point may be helpful.
            // A typical case is when you are using obsoleted Nerdbank.FullDuplexStream,
            // where Writer.WriteAsync may cause the counterpart ReadAsync to immediately finish operation on the same thread,
            // and if you are disposing ByLineTextMessageWriter after ReadAsync, you will have this assertion failure,
            // because WriteAsync hasn't finished yet.
            Debug.Assert(writerSemaphoreVacant, "Attempt to dispose ByLineTextMessageWriter when WriteAsync hasn't finished yet.");
            if (underlyingStream != null)
            {
                Utility.TryDispose(Writer, this);
            }
            if (!LeaveWriterOpen)
            {
                if (underlyingStream != null)
                {
                    Utility.TryDispose(underlyingStream, this);
                }
                else
                {
                    Utility.TryDispose(Writer, this);
                }
            }
            writerSemaphore.Dispose();
            underlyingStream = null;
            Writer = null;
        }
    }
}
