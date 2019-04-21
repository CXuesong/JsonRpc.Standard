using System;
using System.Collections.Generic;
using System.Text;

namespace JsonRpc.Standard
{
    /// <summary>
    /// Contains event arguments for <see cref="Message"/> related events.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the message that raised the event.
        /// </summary>
        public Message Message { get; }

        public MessageEventArgs(Message message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            Message = message;
        }
    }
}
