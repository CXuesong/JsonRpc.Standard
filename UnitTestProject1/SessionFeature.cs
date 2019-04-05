using System;
using System.Collections.Generic;
using System.Text;
using UnitTestProject1.Helpers;

namespace UnitTestProject1
{
    public class SessionFeature
    {
        /// <summary>
        /// Indicates whether the last <see cref="TestJsonRpcService.Delay"/> operation has finished successfully.
        /// </summary>
        public bool IsLastDelayFinished { get; set; }
    }
}
