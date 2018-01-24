using System;
using System.Diagnostics;

namespace Indy.Phoenix
{
    public class Logger: ILogger
    {
        static readonly TraceSource source = new TraceSource("IndyLog");

        public void Log(string message)
        {
            source.TraceEvent(TraceEventType.Information, 0, message);
        }
    }
}
