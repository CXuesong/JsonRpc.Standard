using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace JsonRpc.Standard
{
    internal class NullLogger : ILogger
    {
        public static readonly NullLogger Default = new NullLogger();

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }

    ///// <summary>
    ///// Predefined logging EventId.
    ///// </summary>
    //public static class EventIds
    //{
        
    //}
}
