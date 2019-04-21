using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Messages;
using JsonRpc.Streams;

namespace JsonRpc.WebSockets
{
    /// <summary>
    /// A <see cref="MessageReader"/> implementation that reads JSON-RPC message from <see cref="WebSocket"/>.
    /// </summary>
    /// <remarks>
    /// You can use this class with <see cref="StreamRpcServerHandler"/> or <see cref="StreamRpcClientHandler"/>.
    /// </remarks>
    public class WebSocketMessageReader : QueuedMessageReader
    {

        private readonly int bufferSize;
        private readonly int maxMessageLength;
        private readonly TaskCompletionSource<WebSocketCloseStatus> webSocketCloseTcs;

        /// <inheritdoc cref="WebSocketMessageReader(WebSocket, int)"/>
        /// <remarks>This implementation allows each message to be as large as 4GB.</remarks>
        public WebSocketMessageReader(WebSocket webSocket) : this(webSocket, 4096, int.MaxValue)
        {
        }

        /// <summary>
        /// Initializes a message reader from <see cref="WebSocket"/>.
        /// </summary>
        /// <param name="webSocket">The WebSocket to read messages from.</param>
        /// <param name="bufferSize">Length of the internal buffer kept by the message reader.</param>
        /// <param name="maxMessageLength">The maximum allowed reading message length (bytes) per JSON-RPC message.</param>
        public WebSocketMessageReader(WebSocket webSocket, int bufferSize, int maxMessageLength)
        {
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (maxMessageLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxMessageLength));
            WebSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            this.bufferSize = bufferSize;
            this.maxMessageLength = maxMessageLength;
            webSocketCloseTcs = new TaskCompletionSource<WebSocketCloseStatus>();
        }

        /// <summary>
        /// Gets the underlying <see cref="WebSocket"/>.
        /// </summary>
        public WebSocket WebSocket { get; }

        /// <summary>
        /// Gets a Task that completes with status code when the underlying WebSocket is closed.
        /// </summary>
        public Task<WebSocketCloseStatus> WebSocketClose => webSocketCloseTcs.Task;

        /// <inheritdoc />
        protected override async Task<Message> ReadDirectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var buffer = new byte[bufferSize];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var result = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.CloseStatus != null)
                        webSocketCloseTcs.TrySetResult(result.CloseStatus.Value);
                    if (result.Count > 0)
                    {
                        if (ms.Length + result.Count > maxMessageLength)
                            // perhaps we need to close the channel now.
                            throw new InvalidOperationException("Received message is too long.");
                        ms.Write(buffer, 0, result.Count);
                    }
                    if (result.EndOfMessage)
                    {
                        if (ms.Length > 0)      // If the socket is closing, we won't receive any data in the reception result.
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            return DeserializeMessage(ms);
                        }
                    }
                    if (result.CloseStatus != null)
                    {
                        // Message not ended, but the socket closed.
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Loads a JSON-RPC message from the specified stream.
        /// </summary>
        /// <param name="stream">A stream containing the WebSocket message received from client.</param>
        /// <returns>The parsed JSON-RPC message.</returns>
        protected virtual Message DeserializeMessage(Stream stream)
        {
            // Use UTF-8 by default.
            using (var reader = new StreamReader(stream))
            {
                return Message.LoadJson(reader);
            }
        }
    }

}
