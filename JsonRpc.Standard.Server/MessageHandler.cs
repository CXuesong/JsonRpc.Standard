using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace JsonRpc.Standard.Server
{
    public class MessageHandler
    {
        private readonly MessageReader _MessageReader;
        private readonly MessageWriter _MessageWriter;
        private volatile bool _IsListening;
        private readonly object syncLock = new object();
        private volatile CancellationTokenSource listenCts;

        /// <inheritdoc />
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public MessageHandler(MessageReader messageReader, MessageWriter messageWriter)
        {
            if (messageReader == null) throw new ArgumentNullException(nameof(messageReader));
            if (messageWriter == null) throw new ArgumentNullException(nameof(messageWriter));
            _MessageReader = messageReader;
            _MessageWriter = messageWriter;
        }

        /// <summary>
        /// Creates a <see cref="Connection"/> from <see cref="Stream"/>s.
        /// </summary>
        public static Connection FromStreams(Stream inStream, Stream outStream)
        {
            return FromStreams(inStream, outStream, null);
        }

        /// <summary>
        /// Creates a <see cref="Connection"/> from <see cref="Stream"/>s.
        /// </summary>
        public static Connection FromStreams(Stream inStream, Stream outStream, IStreamMessageLogger logger)
        {
            if (inStream == null) throw new ArgumentNullException(nameof(inStream));
            if (outStream == null) throw new ArgumentNullException(nameof(outStream));
            return new Connection(new StreamMessageReader(inStream, logger), new StreamMessageWriter(outStream, logger));
        }
    }
}
