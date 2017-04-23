using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;

namespace JsonRpc.Standard.Client
{
    /// <summary>
    /// Used to compose and send JSON RPC requests.
    /// </summary>
    public class JsonRpcClient
    {
        private readonly string requestIdPrefix;
        private int requestIdCounter = 0;

        /// <summary>
        /// Initializes a JSON RPC client.
        /// </summary>
        /// <param name="reader">The <see cref="MessageReader"/> used to retrieve the response of the requests.</param>
        /// <param name="writer">The <see cref="MessageWriter"/> used to send requests.</param>
        public JsonRpcClient(MessageReader reader, MessageWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            Reader = reader;
            Writer = writer;
            requestIdPrefix = RuntimeHelpers.GetHashCode(this) + "#";
        }

        /// <summary>
        /// The <see cref="MessageReader"/> used to retrieve the response of the requests.
        /// </summary>
        public MessageReader Reader { get; }

        /// <summary>
        /// The <see cref="MessageWriter"/> used to send requests.
        /// </summary>
        public MessageWriter Writer { get; }

        /// <summary>
        /// Gets the next unique value that can be used as <see cref="RequestMessage.Id"/>.
        /// </summary>
        public object NextRequestId()
        {
            var ct = Interlocked.Increment(ref requestIdCounter);
            return requestIdPrefix + ct;
        }

        /// <summary>
        /// Asynchronously send the request message.
        /// </summary>
        /// <param name="request">The request message to be sent.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task contains the response of the request, or that contains <c>null</c> if <see cref="Reader"/> is <c>null</c>.</returns>
        public Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<ResponseMessage>(cancellationToken);
            return SendAsyncCore(request, cancellationToken);
        }

        internal async Task<ResponseMessage> SendAsyncCore(RequestMessage request, CancellationToken cancellationToken)
        {
            Debug.Assert(request != null);
            await Writer.WriteAsync(request, cancellationToken);
            if (Reader == null) return null;
            var id = request.Id;
            var response = await Reader.ReadAsync(m => m is ResponseMessage resp && Equals(resp.Id, id),
                cancellationToken);
            return (ResponseMessage)response;
        }

        /// <summary>
        /// Asynchronously send the notification message.
        /// </summary>
        /// <param name="request">The notification message to be sent.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that finishes when the message has been sent.</returns>
        public Task SendAsync(NotificationMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
            return Writer.WriteAsync(request, cancellationToken);
        }
    }
}
