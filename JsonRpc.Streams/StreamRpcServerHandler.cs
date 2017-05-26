using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Server;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Provides options for <see cref="JsonRpcServiceHost"/>.
    /// </summary>
    [Flags]
    public enum StreamRpcServerHandlerOptions
    {
        /// <summary>
        /// No options.
        /// </summary>
        None = 0,

        /// <summary>
        /// Makes the response sequence consistent with the request order.
        /// </summary>
        ConsistentResponseSequence,

        /// <summary>
        /// Enables request cancellation via <see cref="StreamRpcServerHandler.TryCancelRequest"/>.
        /// </summary>
        SupportsRequestCancellation
    }

    /// <summary>
    /// A request server handler that uses <see cref="MessageReader"/> and <see cref="MessageWriter"/>
    /// to receive the requests, dispatch them, and send the responses.
    /// </summary>
    public class StreamRpcServerHandler : JsonRpcServerHandler
    {
        private MessageWriter writer;
        private readonly ConcurrentDictionary<MessageId, CancellationTokenSource> requestCtsDict;
        //private readonly Queue<ResponseSlot> responseQueue;
        private readonly bool preserveResponseOrder;

        public StreamRpcServerHandler(IJsonRpcServiceHost serviceHost) : this(serviceHost,
            StreamRpcServerHandlerOptions.None)
        {
        }

        public StreamRpcServerHandler(IJsonRpcServiceHost serviceHost,
            StreamRpcServerHandlerOptions options) : base(serviceHost)
        {
            Options = options;
            requestCtsDict = (options & StreamRpcServerHandlerOptions.SupportsRequestCancellation) ==
                             StreamRpcServerHandlerOptions.SupportsRequestCancellation
                ? new ConcurrentDictionary<MessageId, CancellationTokenSource>()
                : null;
            preserveResponseOrder = (options & StreamRpcServerHandlerOptions.ConsistentResponseSequence) ==
                                    StreamRpcServerHandlerOptions.ConsistentResponseSequence;
        }

        /// <summary>
        /// Server options.
        /// </summary>
        public StreamRpcServerHandlerOptions Options { get; }

        /// <summary>
        /// Attaches <see cref="MessageReader"/> and/or <see cref="MessageWriter"/> to the handler.
        /// </summary>
        /// <returns>A <see cref="IDisposable"/> that detaches the handlers when disposed.</returns>
        public IDisposable Attach(MessageReader reader, MessageWriter writer)
        {
            if (reader == null && writer == null)
                throw new ArgumentException("Either inStream or outStream should not be null.");
            if (writer != null && this.writer != null)
                throw new NotSupportedException("Attaching to multiple writer is not supported.");
            CancellationTokenSource cts = null;
            if (reader != null)
            {
                cts = new CancellationTokenSource();
                var pumpTask = ReaderPumpAsync(reader, cts.Token);
            }
            if (writer != null) this.writer = writer;
            return new MyDisposable(this, cts, writer);
        }

        /// <summary>
        /// Tries to cancel the specified request by request id.
        /// </summary>
        /// <param name="id">Id of the request to cancel.</param>
        /// <exception cref="NotSupportedException"><see cref="StreamRpcServerHandler.SupportsRequestCancellation"/> is not specified in the constructor, so cancellation is not supported.</exception>
        /// <returns><c>true</c> if the specified request has been cancelled. <c>false</c> if the specified request id has not found.</returns>
        /// <remarks>If cancellation is supported, you may cancel an arbitary request in the <see cref="JsonRpcService"/> via <see cref="IRequestCancellationFeature"/>.</remarks>
        public bool TryCancelRequest(MessageId id)
        {
            if (requestCtsDict == null) throw new NotSupportedException("Request cancellation is not supported.");
            CancellationTokenSource cts;
            if (!requestCtsDict.TryRemove(id, out cts)) return false;
            cts.Cancel();
            return true;
        }

        private async Task ReaderPumpAsync(MessageReader reader, CancellationToken ct)
        {
            Task lastRequestTask = null;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var request = (RequestMessage) await reader.ReadAsync(m => m is RequestMessage, ct)
                    .ConfigureAwait(false);
                if (request == null) return; // EOF reached.
                CancellationTokenSource cts = null;
                if (requestCtsDict != null && !request.IsNotification)
                {
                    cts = new CancellationTokenSource();
                    if (!requestCtsDict.TryAdd(request.Id, cts))
                    {
                        //logger.LogWarning(1001, ex, "Duplicate request id for client detected: Id = {id}",
                        //    requestId);
                        cts.Dispose();
                        cts = null;
                    }
                }
                var task = ProcessRequestAsync(request, cts, lastRequestTask);
                if (preserveResponseOrder && !request.IsNotification) lastRequestTask = task;
            }
        }

        private async Task ProcessRequestAsync(RequestMessage message, CancellationTokenSource clientCancellation, Task waitFor)
        {
            var ct = clientCancellation?.Token ?? CancellationToken.None;
            var requestId = message.Id; // Defensive copy, in case request has been changed in the pipeline.
            try
            {
                var result = await ServiceHost.InvokeAsync(message, DefaultFeatures, ct).ConfigureAwait(false);
                if (result == null) return;
                // Wait for the previous task to finish.
                if (waitFor != null) await waitFor.ConfigureAwait(false);
                // Note that cts only reflects client's cancellation request.
                // We still need to write the whole response, if it exists.
                if (writer != null) await writer.WriteAsync(result, ct).ConfigureAwait(false);
            }
            finally
            {
                if (clientCancellation != null)
                {
                    requestCtsDict.TryRemove(requestId, out var cts);
                    Debug.Assert(clientCancellation == cts);
                    clientCancellation.Dispose();
                }
            }
        }

        private class MyDisposable : IDisposable
        {
            private StreamRpcServerHandler owner;
            private CancellationTokenSource readerCts;
            private MessageWriter writer;

            public MyDisposable(StreamRpcServerHandler owner, CancellationTokenSource readerCts, MessageWriter writer)
            {
                Debug.Assert(owner != null);
                Debug.Assert(readerCts != null || writer != null);
                this.owner = owner;
                this.readerCts = readerCts;
                this.writer = writer;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                if (owner == null) return;
                readerCts?.Cancel();
                if (owner.writer == writer) owner.writer = null;
                readerCts = null;
                writer = null;
                owner = null;
            }
        }

        private class ResponseSlot
        {
            private readonly TaskCompletionSource<ResponseMessage> tcs = new TaskCompletionSource<ResponseMessage>();

            public ResponseSlot(MessageId id)
            {
                Id = id;
            }

            public MessageId Id { get; }

            public Task<ResponseMessage> ResponseTask => tcs.Task;

            public bool TrySetResult(ResponseMessage message)
            {
                return tcs.TrySetResult(message);
            }
        }

        private class RequestCancellationFeature : IRequestCancellationFeature
        {
            private readonly StreamRpcServerHandler _Owner;

            public RequestCancellationFeature(StreamRpcServerHandler owner)
            {
                Debug.Assert(owner != null);
                _Owner = owner;
            }

            /// <inheritdoc />
            public bool TryCancel(MessageId id)
            {
                return _Owner.TryCancelRequest(id);
            }
        }

    }
}
