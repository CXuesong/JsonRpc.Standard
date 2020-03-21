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
#if BCL_FEATURE_SERIALIZATION
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

#if BCL_FEATURE_SERIALIZATION
        protected MessageReaderException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif

    }
}
