using System.Collections.Generic;
using System.Threading;
using JsonRpc.Standard.Server;

namespace ConsoleTestApp
{
    public class LibrarySession : Session
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public List<Book> Books { get; } = new List<Book>();

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> that can stop the server.
        /// </summary>
        public CancellationToken CancellationToken => cts.Token;

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void StopServer()
        {
            cts.Cancel();
        }

    }
}