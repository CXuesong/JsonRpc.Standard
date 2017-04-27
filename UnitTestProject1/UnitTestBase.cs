using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class UnitTestBase : IDisposable
    {
        public UnitTestBase(ITestOutputHelper output)
        {
            Output = output;
        }

        /// <inheritdoc />
        public virtual void Dispose()
        {

        }

        public ITestOutputHelper Output { get; }
    }
}
