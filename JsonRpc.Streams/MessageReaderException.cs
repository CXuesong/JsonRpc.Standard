using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JsonRpc.Streams
{
    /// <summary>
    /// An exception that indicates the wrong message reader status.
    /// </summary>
#if NET45
    [Serializable]
#endif
    public class MessageReaderException : Exception
    {
        public MessageReaderException() : this(null, null)
        {
        }

        public MessageReaderException(string message) : this(message, null)
        {
        }

        public MessageReaderException(string message, Exception inner) : base(message, inner)
        {
        }

#if NET45
        protected MessageReaderException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif

    }
}
