using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace JsonRpc.Standard.Server
{
    public interface ISession
    {
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

        public Session(string id)
        {
            Id = id;
        }

        /// <inheritdoc />
        public string Id { get; }
    }
}
