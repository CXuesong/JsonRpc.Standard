using System;
using System.Collections.Generic;
using System.Text;

namespace JsonRpc.Standard.Server
{
    /// <summary>
    /// Provides methods to cancel an arbitrary impending request.
    /// </summary>
    public interface IRequestCancellationFeature
    {
        /// <summary>
        /// Tries to cancel the request with specified request id.
        /// </summary>
        /// <param name="id">The message id to cancel.</param>
        /// <returns><c>true</c>, if the request has been successfully cancelled; <c>false</c> otherwise.</returns>
        bool TryCancel(MessageId id);
    }
}
