using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Reads JSON RPC messages from a <see cref="Stream"/>,
    /// in the format specified in Microsoft Language Server Protocol
    /// (https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md).
    /// </summary>
    public class PartwiseStreamMessageReader : BufferedMessageReader
    {
        private const int headerBufferSize = 1024;
        private const int contentBufferSize = 4 * 1024;

        private static readonly byte[] headerTerminationSequence = {0x0d, 0x0a, 0x0d, 0x0a};

        public PartwiseStreamMessageReader(Stream stream) : this(stream, Encoding.UTF8, null)
        {

        }

        public PartwiseStreamMessageReader(Stream stream, IStreamMessageLogger messageLogger) : this(stream,
            Encoding.UTF8,
            messageLogger)
        {

        }

        public PartwiseStreamMessageReader(Stream stream, Encoding encoding, IStreamMessageLogger messageLogger)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            BaseStream = stream;
            Encoding = encoding;
            MessageLogger = messageLogger;
        }

        public IStreamMessageLogger MessageLogger { get; }

        public Stream BaseStream { get; }

        public Encoding Encoding { get; }

        // Used to store the exceeded content during last read.
        private readonly List<byte> headerBuffer = new List<byte>(headerBufferSize);

        /// <inheritdoc />
        protected override async Task<Message> ReadMessageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int termination;
            int contentLength;
            while ((termination = headerBuffer.IndexOf(headerTerminationSequence)) < 0)
            {
                // Read until \r\n\r\n is found.
                var headerSubBuffer = new byte[headerBufferSize];
                int readLength;
                try
                {
                    readLength = await BaseStream.ReadAsync(headerSubBuffer, 0, headerBufferSize, cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    // Throws OperationCanceledException if the cancellation has already been requested.
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                if (readLength == 0)
                {
                    if (headerBuffer.Count == 0)
                        return null; // EOF
                    else
                        throw new JsonRpcException("Unexpected EOF when reading header.");
                }
                headerBuffer.AddRange(headerSubBuffer.Take(readLength));
            }
            // Parse headers.
            var headerBytes = new byte[termination];
            headerBuffer.CopyTo(0, headerBytes, 0, termination);
            var header = Encoding.GetString(headerBytes, 0, termination);
            var headers = header
                .Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None)
                .Select(s => s.Split(new[] {": "}, 2, StringSplitOptions.None));
            try
            {
                contentLength = Convert.ToInt32(headers.First(e => e[0] == "Content-Length")[1]);
            }
            catch (InvalidOperationException)
            {
                throw new JsonRpcException("Invalid JSON RPC header. Content-Length is missing.");
            }
            catch (FormatException)
            {
                throw new JsonRpcException("Invalid JSON RPC header. Content-Length is invalid.");
            }
            if (contentLength <= 0)
                throw new JsonRpcException("Invalid JSON RPC header. Content-Length is invalid.");
            // Concatenate and read the rest of the content.
            var contentBuffer = new byte[contentLength];
            var contentOffset = termination + headerTerminationSequence.Length;
            if (headerBuffer.Count > contentOffset + contentLength)
            {
                // We have read too more bytes than contentLength specified
                headerBuffer.CopyTo(contentOffset, contentBuffer, 0, contentLength);
                // Trim excess
                headerBuffer.RemoveRange(0, contentOffset + contentLength);
            }
            else
            {
                // We need to read more bytes…
                headerBuffer.CopyTo(contentOffset, contentBuffer, 0, headerBuffer.Count - contentOffset);
                var pos = headerBuffer.Count - contentOffset; // The position to put the next character.
                headerBuffer.Clear();
                try
                {
                    while (pos < contentLength)
                    {
                        var length = BaseStream.Read(contentBuffer, pos,
                            Math.Min(contentLength - pos, contentBufferSize));
                        if (length == 0) throw new JsonRpcException("Unexpected EOF when reading content.");
                        pos += length;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Throws OperationCanceledException if the cancellation has already been requested.
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
            }
            // Release the semaphore ASAP.
            // Deserialization
            using (var ms = new MemoryStream(contentBuffer))
            {
                using (var sr = new StreamReader(ms, Encoding))
                {
                    if (MessageLogger != null)
                    {
                        var content = sr.ReadToEnd();
                        MessageLogger.NotifyMessageReceived(content);
                        return RpcSerializer.DeserializeMessage(content);
                    }
                    else
                    {
                        return RpcSerializer.DeserializeMessage(sr);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Provides logger of messages for diagnostic purpose.
    /// </summary>
    [Obsolete("This type will be remvoed in the future.")]
    public interface IStreamMessageLogger
    {
        void NotifyMessageSent(string content);

        void NotifyMessageReceived(string content);
    }

    [Obsolete("This type will be remvoed in the future.")]
    public class StreamMessageLogger : IStreamMessageLogger
    {
        private readonly Action<string> _NotifyMessageSent;
        private readonly Action<string> _NotifyMessageReceived;

        public StreamMessageLogger(Action<string> notifyMessageSent, Action<string> notifyMessageReceived)
        {
            _NotifyMessageSent = notifyMessageSent;
            _NotifyMessageReceived = notifyMessageReceived;
        }

        /// <inheritdoc />
        public void NotifyMessageSent(string content)
        {
            _NotifyMessageSent?.Invoke(content);
        }

        /// <inheritdoc />
        public void NotifyMessageReceived(string content)
        {
            _NotifyMessageReceived?.Invoke(content);
        }
    }
}
