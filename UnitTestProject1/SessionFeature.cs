using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTestProject1
{
    public class SessionFeature
    {
        /// <summary>
        /// Indicates whether the last <see cref="TestJsonRpcService.Delay"/> operation has finished successfuly.
        /// </summary>
        public bool IsLastDelayFinished { get; set; }
    }
}
