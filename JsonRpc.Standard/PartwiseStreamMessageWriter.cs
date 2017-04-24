using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Writes JSON RPC messages to a <see cref="Stream"/>,
    /// in the format specified in Microsoft Language Server Protocol
    /// (https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md).
    /// </summary>
    public class PartwiseStreamMessageWriter : MessageWriter
    {
        private static readonly UTF8Encoding UTF8NoBom = new UTF8Encoding(false, true);

        private readonly SemaphoreSlim streamSemaphore = new SemaphoreSlim(1, 1);

        public PartwiseStreamMessageWriter(Stream stream)
            : this(stream, UTF8NoBom, null)
        {
        }

        public PartwiseStreamMessageWriter(Stream stream, IStreamMessageLogger messageLogger) : this(stream, UTF8NoBom,
            messageLogger)
        {
        }

        public PartwiseStreamMessageWriter(Stream stream, Encoding encoding, IStreamMessageLogger messageLogger)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            BaseStream = stream;
            Encoding = encoding;
            MessageLogger = messageLogger;
        }

        public Stream BaseStream { get; }

        public Encoding Encoding { get; }

        public string ContentType { get; set; }

        public IStreamMessageLogger MessageLogger { get; }

        /// <inheritdoc />
        public override async Task WriteAsync(Message message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms, Encoding, 4096, true))
                {
                    if (MessageLogger == null)
                    {
                        RpcSerializer.SerializeMessage(writer, message);
                    }
                    else
                    {
                        var content = RpcSerializer.SerializeMessage(message);
                        MessageLogger.NotifyMessageSent(content);
                        writer.Write(content);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                using (await streamSemaphore.LockAsync(cancellationToken))
                {
                    try
                    {
                        using (var writer = new StreamWriter(BaseStream, Encoding, 4096, true))
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
                        await ms.CopyToAsync(BaseStream, 81920, cancellationToken);
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
    }
}
