using System;
using System.Threading;

namespace JsonRpc.Standard.Contracts
{
    /// <summary>
    /// Provides basic session data.
    /// </summary>
    /// <remarks>
    /// It's recommended that you use the session id as a key, and store the actual runtime state in other
    /// global objects (using DI or static object), especially in ASP.NET Core. However, it's still possible
    /// to implement your own session object with and explicitly assigns it during the application startup.
    /// </remarks>
    public interface ISession
    {
        /// <summary>A unique identifier for the current session.</summary>
        string Id { get; }
    }

    public class Session : ISession
    {
        private static int counter = 0;

        private static string NextId
        {
            get
            {
                var ct = Interlocked.Increment(ref counter);
                return "UnnamedSession" + ct;
            }
        }

        public Session() : this(NextId)
        {
            
        }

        /// <summary>
        /// Initialize with a specified session ID.
        /// </summary>
        /// <param name="id">Session ID.</param>
        /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
        public Session(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            Id = id;
        }

        /// <inheritdoc />
        public string Id { get; }
    }
}
