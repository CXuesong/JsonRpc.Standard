using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class UnitTestBase : IDisposable
    {
        public UnitTestBase(ITestOutputHelper output)
        {
            Output = output;
            LoggerFactory = new LoggerFactory();
            if (output != null)
            {
                LoggerFactory.AddProvider(new TestLoggerProvider(output));
            }
        }

        /// <inheritdoc />
        public virtual void Dispose()
        {
            LoggerFactory.Dispose();
        }

        public ITestOutputHelper Output { get; }

        public ILoggerFactory LoggerFactory { get; }
    }
}
