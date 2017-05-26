using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;

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
        private Encoding _Encoding;

        public PartwiseStreamMessageWriter(Stream stream) : this(stream, Utility.UTF8NoBom)
        {
        }

        public PartwiseStreamMessageWriter(Stream stream, Encoding encoding)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            Stream = stream;
            Encoding = encoding;
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
        public string ContentType { get; set; }

        /// <summary>
        /// Whether to leave <see cref="Stream"/> open when disposing this instance.
        /// </summary>
        public bool LeaveStreamOpen { get; set; }

        /// <inheritdoc />
        public override async Task WriteAsync(Message message, CancellationToken cancellationToken)
        {
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
                    await streamSemaphore.WaitAsync(linkedTokenSource.Token);
                    try
                    {
                        using (var writer = new StreamWriter(Stream, Encoding, 4096, true))
                        {
                            await writer.WriteAsync("Content-Length: ");
                            await writer.WriteAsync(ms.Length.ToString());
                            await writer.WriteAsync("\r\n");
                            await writer.WriteAsync("Content-Type: ");
                            await writer.WriteAsync(ContentType);
                            await writer.WriteAsync("\r\n\r\n");
                            await writer.FlushAsync();
                        }
                        ms.Seek(0, SeekOrigin.Begin);
                        await ms.CopyToAsync(Stream, 81920, linkedTokenSource.Token);
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
