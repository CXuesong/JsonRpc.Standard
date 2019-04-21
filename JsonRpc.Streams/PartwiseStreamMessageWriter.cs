using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Writes JSON RPC messages to a <see cref="System.IO.Stream"/>,
    /// in the format specified in Microsoft Language Server Protocol
    /// (https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md).
    /// </summary>
    public class PartwiseStreamMessageWriter : MessageWriter
    {

        private readonly SemaphoreSlim streamSemaphore = new SemaphoreSlim(1, 1);
        private Encoding _Encoding = Utility.UTF8NoBom;

        /// <summary>
        /// Initializes a message writer from <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The stream to write messages to.</param>
        public PartwiseStreamMessageWriter(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            Stream = stream;
        }

        /// <summary>
        /// The underlying stream to write messages into.
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// Encoding of the emitted messages.
        /// </summary>
        public Encoding Encoding
        {
            get => _Encoding;
            set => _Encoding = value ?? Utility.UTF8NoBom;
        }

        /// <summary>
        /// Content-Type header value of the emitted messages.
        /// </summary>
        /// <value>Content-Type header value, or <c>null</c> to suppress Content-Type header.</value>
        public string ContentType { get; set; } = "application/json-rpc";

        /// <summary>
        /// Whether to follow the <see cref="ContentType"/> with a "charset=xxx" part
        /// when writing messages to the stream. This property has no effect if <see cref="ContentType"/>
        /// is null.
        /// </summary>
        public bool EmitContentCharset { get; set; } = true;

        /// <summary>
        /// Whether to leave <see cref="Stream"/> open when disposing this instance.
        /// </summary>
        public bool LeaveStreamOpen { get; set; }

        /// <inheritdoc />
        public override async Task WriteAsync(Message message, CancellationToken cancellationToken)
        {
            // Ensure that a message is either written completely or not at all.
            if (message == null) throw new ArgumentNullException(nameof(message));
            cancellationToken.ThrowIfCancellationRequested();
            DisposalToken.ThrowIfCancellationRequested();
            using (var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposalToken))
            using (var ms = new MemoryStream())
            {
                try
                {
                    using (var writer = new StreamWriter(ms, Encoding, 4096, true)) message.WriteJson(writer);
                    linkedTokenSource.Token.ThrowIfCancellationRequested();
                    await streamSemaphore.WaitAsync(linkedTokenSource.Token).ConfigureAwait(false);
                    try
                    {
                        using (var writer = new StreamWriter(Stream, Encoding, 4096, true))
                        {
                            await writer.WriteAsync("Content-Length: ").ConfigureAwait(false);
                            await writer.WriteAsync(ms.Length.ToString()).ConfigureAwait(false);
                            await writer.WriteAsync("\r\n").ConfigureAwait(false);
                            if (ContentType != null)
                            {
                                await writer.WriteAsync("Content-Type: ").ConfigureAwait(false);
                                await writer.WriteAsync(ContentType).ConfigureAwait(false);
                                if (EmitContentCharset)
                                {
                                    await writer.WriteAsync(";charset=").ConfigureAwait(false);
                                    await writer.WriteAsync(Encoding.WebName).ConfigureAwait(false);
                                }
                                await writer.WriteAsync("\r\n").ConfigureAwait(false);
                            }
                            await writer.WriteAsync("\r\n").ConfigureAwait(false);
                            await writer.FlushAsync().ConfigureAwait(false);
                        }
                        ms.Seek(0, SeekOrigin.Begin);
                        // ReSharper disable once MethodSupportsCancellation
                        await ms.CopyToAsync(Stream, 81920 /*, linkedTokenSource.Token*/).ConfigureAwait(false);
                    }
                    finally
                    {
                        streamSemaphore.Release();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Throws OperationCanceledException if the cancellation has already been requested.
                    linkedTokenSource.Token.ThrowIfCancellationRequested();
                    throw;
                }
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Stream == null) return;
            if (!LeaveStreamOpen) Stream.Dispose();
            Stream = null;
        }
    }
}
