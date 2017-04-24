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
    /// Provides options for <see cref="JsonRpcClient"/>.
    /// </summary>
    [Flags]
    public enum JsonRpcClientOptions
    {
        /// <summary>
        /// No special configurations.
        /// </summary>
        None = 0,

        /// <summary>
        /// Preserves the responses whose Id dosn't match any requests sent by this client.
        /// The default behavior will just discard them.
        /// </summary>
        PreserveForeignResponses
    }


    /// <summary>
    /// Used to compose and send JSON RPC requests.
    /// </summary>
    public class JsonRpcClient
    {
        private readonly string requestIdPrefix;
        private int requestIdCounter = 0;
        private Dictionary<object, RequestMessage> impendingRequestDict = new Dictionary<object, RequestMessage>();

        /// <summary>
        /// Initializes a JSON RPC client.
        /// </summary>
        /// <param name="reader">The <see cref="MessageReader"/> used to retrieve the response of the requests.</param>
        /// <param name="writer">The <see cref="MessageWriter"/> used to send requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <c>null</c>.</exception>
        public JsonRpcClient(MessageReader reader, MessageWriter writer) : this(reader, writer, JsonRpcClientOptions.None)
        {
        }

        /// <summary>
        /// Initializes a JSON RPC client.
        /// </summary>
        /// <param name="reader">The <see cref="MessageReader"/> used to retrieve the response of the requests.</param>
        /// <param name="writer">The <see cref="MessageWriter"/> used to send requests.</param>
        /// <param name="options">Client options.</param>
        /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <c>null</c>.</exception>
        public JsonRpcClient(MessageReader reader, MessageWriter writer, JsonRpcClientOptions options)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            Reader = reader;
            Writer = writer;
            Options = options;
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
        /// Client options.
        /// </summary>
        public JsonRpcClientOptions Options { get; }

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
        public Task<ResponseMessage> SendAsync(GeneralRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<ResponseMessage>(cancellationToken);
            return SendAsyncCore(request, cancellationToken);
        }

        internal async Task<ResponseMessage> SendAsyncCore(GeneralRequestMessage message,
            CancellationToken cancellationToken)
        {
            Debug.Assert(message != null);
            var request = message as RequestMessage;
            if (Reader != null && request != null)
            {
                lock (impendingRequestDict) impendingRequestDict.Add(request.Id, request);
            }
            await Writer.WriteAsync(message, cancellationToken);
            if (Reader == null || request == null) return null;
            // Wait for response.
            var id = request.Id;
            try
            {
                if ((Options & JsonRpcClientOptions.PreserveForeignResponses) ==
                    JsonRpcClientOptions.PreserveForeignResponses)
                {
                    var response = (ResponseMessage) await Reader.ReadAsync(m =>
                            m is ResponseMessage resp && Equals(resp.Id, id),
                        cancellationToken);
                    return response;
                }
                else
                {
                    // Discard all the responses that are not in the dictionary.
                    while (true)
                    {
                        var response = (ResponseMessage) await Reader.ReadAsync(m =>
                            {
                                if (!(m is ResponseMessage resp)) return false;
                                if (Equals(resp.Id, id)) return true;
                                lock (impendingRequestDict) return !impendingRequestDict.ContainsKey(resp.Id);
                            },
                            cancellationToken);
                        if (object.Equals(response.Id, id)) return response;
                    }
                }
            }
            finally
            {
                lock (impendingRequestDict) impendingRequestDict.Remove(id);
            }
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
