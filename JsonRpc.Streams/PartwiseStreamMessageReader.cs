using JsonRpc.Standard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Reads JSON RPC messages from a <see cref="System.IO.Stream"/>,
    /// in the format specified in Microsoft Language Server Protocol
    /// (https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md).
    /// </summary>
    public class PartwiseStreamMessageReader : QueuedMessageReader
    {
        private const int headerBufferSize = 1024;
        private const int contentBufferSize = 4 * 1024;

        private static readonly byte[] headerTerminationSequence = {0x0d, 0x0a, 0x0d, 0x0a};

        private Encoding _Encoding = Encoding.UTF8;

        /// <summary>
        /// Initializes a message reader from <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The stream to read messages from.</param>
        public PartwiseStreamMessageReader(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            Stream = stream;
        }

        /// <summary>
        /// The underlying stream to read messages from.
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// Default encoding of the received messages.
        /// </summary>
        /// <remarks>
        /// If "charset" part is detected in the header part of the message, the specified charset will be used.
        /// </remarks>
        public Encoding Encoding
        {
            get => _Encoding;
            set => _Encoding = value ?? Encoding.UTF8;
        }

        /// <summary>
        /// Whether to leave <see cref="Stream"/> open when disposing this instance.
        /// </summary>
        public bool LeaveStreamOpen { get; set; }

        // Used to store the exceeded content during last read.
        private List<byte> headerBuffer = new List<byte>(headerBufferSize);

        /// <summary>
        /// Directly reads a message out of the <see cref="Stream"/>.
        /// </summary>
        /// <exception cref="OperationCanceledException">The operation has been cancelled before a message has been read.</exception>
        protected override async Task<Message> ReadDirectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int terminationPos;
            while ((terminationPos = headerBuffer.IndexOf(headerTerminationSequence)) < 0)
            {
                // Read until \r\n\r\n is found.
                var headerSubBuffer = new byte[headerBufferSize];
                var readLength = await Stream.ReadAsync(headerSubBuffer, 0, headerBufferSize, cancellationToken);
                if (readLength == 0)
                {
                    if (headerBuffer.Count == 0)
                        return null; // EOF
                    else
                        throw new MessageReaderException("Unexpected EOF when reading header.");
                }
                headerBuffer.AddRange(headerSubBuffer.Take(readLength));
            }
            // Parse headers.
            var headerBytes = new byte[terminationPos];
            headerBuffer.CopyTo(0, headerBytes, 0, terminationPos);
            var header = Encoding.GetString(headerBytes, 0, terminationPos);
            var headers = header
                .Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None)
                .Select(s => s.Split(new[] {": "}, 2, StringSplitOptions.None))
                .ToArray();
            int contentLength;
            try
            {
                contentLength = Convert.ToInt32(headers.First(e => e[0] == "Content-Length")[1]);
            }
            catch (InvalidOperationException)
            {
                throw new MessageReaderException("Invalid JSON RPC header. Content-Length is missing.");
            }
            catch (FormatException)
            {
                throw new MessageReaderException("Invalid JSON RPC header. Content-Length is invalid.");
            }
            if (contentLength <= 0)
                throw new MessageReaderException("Invalid JSON RPC header. Content-Length is invalid.");
            var contentType = headers.FirstOrDefault(e => e[0] == "Content-Type")?[1];
            var contentEncoding = Encoding;
            if (!string.IsNullOrEmpty(contentType))
            {
                var mediaType = MediaTypeHeaderValue.Parse(contentType);
                if (mediaType.CharSet != null)
                {
                    // Compatibility for LSP
                    // See https://github.com/Microsoft/language-server-protocol/pull/199 .
                    if (string.Equals(mediaType.CharSet, "utf8", StringComparison.OrdinalIgnoreCase))
                    {
                        contentEncoding = Utility.UTF8NoBom;
                    }
                    else
                    {
                        try
                        {
                            contentEncoding = Encoding.GetEncoding(mediaType.CharSet);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new MessageReaderException(
                                "Invalid JSON RPC header. Cannot recognize Content-Type(charset).", ex);
                        }
                    }
                }
            }
            // Concatenate and read the rest of the content.
            var contentBuffer = new byte[contentLength];
            var contentOffset = terminationPos + headerTerminationSequence.Length;
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
                    var length = Stream.Read(contentBuffer, pos,
                        Math.Min(contentLength - pos, contentBufferSize));
                    if (length == 0) throw new MessageReaderException("Unexpected EOF when reading content.");
                    pos += length;
                }
            }
            // Deserialization
            try
            {
                using (var ms = new MemoryStream(contentBuffer))
                {
                    using (var sr = new StreamReader(ms, contentEncoding))
                    {
                        return Message.LoadJson(sr);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new MessageReaderException("Cannot parse the message body into a valid JSON RPC message.", ex);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Stream == null) return;
            if (!LeaveStreamOpen) Stream.Dispose();
            Stream = null;
            headerBuffer = null;
        }
    }
}
