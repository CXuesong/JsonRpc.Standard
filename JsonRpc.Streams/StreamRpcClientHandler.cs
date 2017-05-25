using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;

namespace JsonRpc.Streams
{

    /// <summary>
    /// Provides options for <see cref="JsonRpcClient"/>.
    /// </summary>
    [Flags]
    public enum StreamRpcClientOptions
    {
        /// <summary>
        /// No special configurations.
        /// </summary>
        None = 0,

        /// <summary>
        /// Preserves the responses whose Id doesn't match any requests sent by this client.
        /// The default behavior will just discard them.
        /// </summary>
        PreserveForeignResponses
    }

    /// <summary>
    /// A client request handler that uses <see cref="MessageReader"/> and <see cref="MessageWriter"/>
    /// to transmit requests and receive responses.
    /// </summary>
    public class StreamRpcClientHandler : JsonRpcClientHandler
    {

        private MessageWriter writer;

        private readonly ConcurrentDictionary<MessageId, TaskCompletionSource<ResponseMessage>> impendingRequestDict
            = new ConcurrentDictionary<MessageId, TaskCompletionSource<ResponseMessage>>();

        public StreamRpcClientHandler() : this(StreamRpcClientOptions.None)
        {
        }

        public StreamRpcClientHandler(StreamRpcClientOptions options)
        {
            Options = options;
        }

        /// <summary>
        /// Client options.
        /// </summary>
        public StreamRpcClientOptions Options { get; }

        /// <summary>
        /// Gets how many non-notification request messages has been sent and is yet to receive the responses.
        /// </summary>
        public int ImpendingRequestCount => impendingRequestDict.Count;

        /// <summary>
        /// Attaches <see cref="MessageReader"/> and/or <see cref="MessageWriter"/> to the handler.
        /// </summary>
        /// <returns>A <see cref="IDisposable"/> that detaches the handlers when disposed.</returns>
        /// <remarks>If you do not attach any <see cref="MessageReader"/>, you must not send any non-notification requests,
        /// as they may lead to non-finishing tasks.</remarks>
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

        private async Task ReaderPumpAsync(MessageReader reader, CancellationToken ct)
        {
            Debug.Assert(reader != null);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var response = (ResponseMessage) await ((Options & StreamRpcClientOptions.PreserveForeignResponses) ==
                                                        StreamRpcClientOptions.PreserveForeignResponses
                    ? reader.ReadAsync(m => m is ResponseMessage r && impendingRequestDict.ContainsKey(r.Id), ct)
                    : reader.ReadAsync(m => m is ResponseMessage, ct));
                if (impendingRequestDict.TryRemove(response.Id, out var tcs)) tcs.TrySetResult(response);
            }
        }

        /// <inheritdoc />
        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (writer == null)
                throw new InvalidOperationException("You need at least to attach a MessageWriter before sending requests.");
            cancellationToken.ThrowIfCancellationRequested();
            var ctr = default(CancellationTokenRegistration);
            TaskCompletionSource<ResponseMessage> tcs = null;
            if (!request.IsNotification)
            {
                var requestId = request.Id; // Defensive copy
                // We need to wait for the the response.
                tcs = new TaskCompletionSource<ResponseMessage>();
                if (!impendingRequestDict.TryAdd(request.Id, tcs))
                    throw new InvalidOperationException(
                        "A request message with the same message ID has been sent and is waiting for response.");
                if (cancellationToken.CanBeCanceled)
                {
                    ctr = cancellationToken.Register(o =>
                    {
                        TaskCompletionSource<ResponseMessage> tcs1;
                        var id = (MessageId) o;
                        if (!impendingRequestDict.TryGetValue(id, out tcs1)) return;
                        if (!tcs1.TrySetCanceled()) return;
                        // Note that server might still send the response after cancellation on the client.
                        // If we are going to keep all "foreign" responses, we need to be able to recgnize it later.
                        var keepRequestIdInMind = (Options & StreamRpcClientOptions.PreserveForeignResponses) ==
                                                  StreamRpcClientOptions.PreserveForeignResponses;
                        if (keepRequestIdInMind)
                        {
#pragma warning disable 4014
                            // ReSharper disable MethodSupportsCancellation
                            Task.Delay(60000)
                                .ContinueWith((prev, o1) =>
                                {
                                    impendingRequestDict.TryRemove((MessageId) o1, out _);
                                }, o);
                            // ReSharper restore MethodSupportsCancellation
#pragma warning restore 4014
                        }
                        else
                        {
                            impendingRequestDict.TryRemove(id, out _);
                        }
                    }, request.Id);
                }
            }
            try
            {
                OnMessageSending(request);
                await writer.WriteAsync(request, cancellationToken);
            }
            catch (Exception)
            {
                // Exception when sending the request…
                if (!request.IsNotification)
                {
                    ctr.Dispose();
                    impendingRequestDict.TryRemove(request.Id, out _);
                }
                throw;
            }
            if (request.IsNotification) return null;
            // Now wait for the response.
            Debug.Assert(tcs != null);
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                ctr.Dispose();
            }
        }

        private class MyDisposable : IDisposable
        {
            private StreamRpcClientHandler owner;
            private CancellationTokenSource readerCts;
            private MessageWriter writer;

            public MyDisposable(StreamRpcClientHandler owner, CancellationTokenSource readerCts, MessageWriter writer)
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
    }
}
