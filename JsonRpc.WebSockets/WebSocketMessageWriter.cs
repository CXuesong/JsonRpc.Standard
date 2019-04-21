using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;
using JsonRpc.Streams;

namespace JsonRpc.WebSockets
{
    /// <summary>
    /// A <see cref="MessageWriter"/> implementation that writes JSON-RPC message into <see cref="WebSocket"/>.
    /// </summary>
    /// <remarks>
    /// You can use this class with <see cref="StreamRpcServerHandler"/> or <see cref="StreamRpcClientHandler"/>.
    /// </remarks>
    public class WebSocketMessageWriter : MessageWriter
    {

        private readonly int chunkSize;

        /// <inheritdoc cref="WebSocketMessageWriter(WebSocket, int)"/>
        public WebSocketMessageWriter(WebSocket webSocket) : this(webSocket, 4096)
        {
        }

        /// <summary>
        /// Initializes a message reader from <see cref="WebSocket"/>.
        /// </summary>
        /// <param name="webSocket">The WebSocket to write messages into.</param>
        /// <param name="chunkSize">The maximum size in bytes of each JSON-RPC frame when sending messages.</param>
        public WebSocketMessageWriter(WebSocket webSocket, int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            WebSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            this.chunkSize = chunkSize;
        }

        /// <summary>
        /// Gets the underlying <see cref="WebSocket"/>.
        /// </summary>
        public WebSocket WebSocket { get; }

        /// <inheritdoc />
        public override async Task WriteAsync(Message message, CancellationToken cancellationToken)
        {
            using (var ms = new MemoryStream())
            {
                DeserializeMessage(ms, message);
                var pos = 0;
                if (!ms.TryGetBuffer(out var buffer))
                {
                    Debug.Fail("We should get buffer successfully.");
                    buffer = new ArraySegment<byte>(ms.ToArray());
                }
                while (pos < buffer.Count)
                {
                    var length = Math.Min(chunkSize, buffer.Count - pos);
                    var isEom = pos + length >= buffer.Count;
                    await WebSocket.SendAsync(new ArraySegment<byte>(buffer.Array, pos, length), WebSocketMessageType.Text, isEom, cancellationToken);
                    pos += length;
                }
            }
        }

        /// <summary>
        /// Writes a JSON-RPC message to the specified stream.
        /// </summary>
        /// <param name="stream">A stream containing the WebSocket message to be sent to the client.</param>
        /// <param name="message">The JSON-RPC message to be sent to the client.</param>
        protected virtual void DeserializeMessage(Stream stream, Message message)
        {
            // Use UTF-8 by default.
            using (var writer = new StreamWriter(stream))
            {
                message.WriteJson(writer);
            }
        }

    }
}
