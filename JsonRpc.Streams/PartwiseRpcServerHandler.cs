using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using JsonRpc.Standard.Server;

namespace JsonRpc.Streams
{
    /// <summary>
    /// Reads JSON RPC messages from a <see cref="Stream"/>,
    /// in the format specified in Microsoft Language Server Protocol
    /// (https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md).
    /// </summary>
    public class PartwiseRpcServerHandler : JsonRpcServerHandler
    {

        private MessageReader reader;
        private MessageWriter writer;

        public PartwiseRpcServerHandler(IJsonRpcServiceHost serviceHost) : base(serviceHost)
        {
            
        }

        public IDisposable Attach(MessageReader reader, MessageWriter writer)
        {
            if (reader == null && writer == null)
                throw new ArgumentException("Either inStream or outStream should not be null.");
            if (reader != null && this.reader != null)
                throw new NotSupportedException("Attaching to multiple readers is not supported.");
            if (writer != null && this.writer != null)
                throw new NotSupportedException("Attaching to multiple writer is not supported.");
            if (reader != null) this.reader = reader;
            if (writer != null) this.writer = writer;
            return new MyDisposable(this, reader, writer);
        }

        private class MyDisposable : IDisposable
        {
            private PartwiseRpcServerHandler owner;
            private MessageReader reader;
            private MessageWriter writer;

            public MyDisposable(PartwiseRpcServerHandler owner, MessageReader reader, MessageWriter writer)
            {
                Debug.Assert(owner != null);
                Debug.Assert(reader != null || writer != null);
                this.owner = owner;
                this.reader = reader;
                this.writer = writer;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                if (owner == null) return;
                if (owner.reader == reader) owner.reader = null;
                if (owner.writer == writer) owner.writer = null;
                reader = null;
                writer = null;
                owner = null;
            }
        }

    }
}
