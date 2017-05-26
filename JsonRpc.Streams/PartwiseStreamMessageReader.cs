using JsonRpc.Standard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Reads JSON RPC messages from a <see cref="Stream"/>,
    /// in the format specified in Microsoft Language Server Protocol
    /// (https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md).
    /// </summary>
    public class PartwiseStreamMessageReader : QueuedMessageReader
    {
        private const int headerBufferSize = 1024;
        private const int contentBufferSize = 4 * 1024;

        private static readonly byte[] headerTerminationSequence = {0x0d, 0x0a, 0x0d, 0x0a};

        public PartwiseStreamMessageReader(Stream stream) : this(stream, Encoding.UTF8)
        {

        }

        public PartwiseStreamMessageReader(Stream stream, Encoding encoding)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            BaseStream = stream;
            Encoding = encoding;
        }

        /// <summary>
        /// The underlying stream to read messages from.
        /// </summary>
        public Stream BaseStream { get; private set; }

        /// <summary>
        /// Default encoding of the received messages.
        /// </summary>
        /// <remarks>
        /// If "charset" part is detected in the header part of the message, the specified charset will be
        /// tried first. If the reader cannot recognize the specified encoding, the default encoding will be used.
        /// </remarks>
        public Encoding Encoding
        {
            get => _Encoding;
            set => _Encoding = value ?? Encoding.UTF8;
        }

        /// <summary>
        /// Whether to leave <see cref="BaseStream"/> open when disposing this instance.
        /// </summary>
        public bool LeaveStreamOpen { get; set; }

        // Used to store the exceeded content during last read.
        private List<byte> headerBuffer = new List<byte>(headerBufferSize);

        private Encoding _Encoding;

        /// <inheritdoc />
        protected override async Task<Message> ReadDirectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int termination;
            int contentLength;
            while ((termination = headerBuffer.IndexOf(headerTerminationSequence)) < 0)
            {
                // Read until \r\n\r\n is found.
                var headerSubBuffer = new byte[headerBufferSize];
                var readLength = await BaseStream.ReadAsync(headerSubBuffer, 0, headerBufferSize, cancellationToken);
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
                while (pos < contentLength)
                {
                    var length = BaseStream.Read(contentBuffer, pos,
                        Math.Min(contentLength - pos, contentBufferSize));
                    if (length == 0) throw new JsonRpcException("Unexpected EOF when reading content.");
                    pos += length;
                }
            }
            // Deserialization
            using (var ms = new MemoryStream(contentBuffer))
            {
                using (var sr = new StreamReader(ms, Encoding))
                    return Message.LoadJson(sr);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (BaseStream == null) return;
            if (!LeaveStreamOpen) BaseStream.Dispose();
            BaseStream = null;
            headerBuffer = null;
        }
    }
}
