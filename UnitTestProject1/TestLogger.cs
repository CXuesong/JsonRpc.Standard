using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class TestLoggerProvider : ILoggerProvider
    {

        public TestLoggerProvider(ITestOutputHelper outputHelper)
        {
            if (outputHelper == null) throw new ArgumentNullException(nameof(outputHelper));
            OutputHelper = outputHelper;
        }

        public ITestOutputHelper OutputHelper { get; }

        /// <inheritdoc />
        public void Dispose()
        {

        }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(OutputHelper, categoryName);
        }
    }

    public class TestLogger : ILogger
    {

        public TestLogger(ITestOutputHelper outputHelper, string name)
        {
            if (outputHelper == null) throw new ArgumentNullException(nameof(outputHelper));
            OutputHelper = outputHelper;
            Name = name;
        }

        public ITestOutputHelper OutputHelper { get; }

        public string Name { get; }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            OutputHelper.WriteLine("{0}: {1}[{2}]\r\n{3}", logLevel, Name, eventId, formatter(state, exception));
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }
}
