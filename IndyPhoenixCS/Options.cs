using System;
using System.Collections.Generic;

namespace Indy.Phoenix
{
    public class Options
    {
        public TimeSpan? Timeout { get; set; }
        public TimeSpan? HeartbeatInterval { get; set; }
        public Func<int, TimeSpan> ReconnectAfter { get; set; }
        public ILogger Logger { get; set; }
        public Dictionary<string, string> Params;


        public class Builder
        {
            Options opt;

            public Builder()
            {
                opt = new Options();
            }

            public Builder Timeout(TimeSpan timeout)
            {
                opt.Timeout = timeout;
                return this;
            }

            public Builder HeartbeatInterval(TimeSpan interval)
            {
                opt.HeartbeatInterval = interval;
                return this;
            }

            public Builder ReconnectAfter(Func<int, TimeSpan> callback)
            {
                opt.ReconnectAfter = callback;
                return this;
            }

            public Builder Logger(ILogger logger)
            {
                opt.Logger = logger;
                return this;
            }

            public Builder Params(Dictionary<string, string> paramsDict)
            {
                opt.Params = paramsDict;
                return this;
            }

            public Options Build()
            {
                var original = opt;
                opt = new Options();
                opt.Timeout = original.Timeout;
                opt.HeartbeatInterval = original.HeartbeatInterval;
                opt.Logger = original.Logger;
                opt.ReconnectAfter = original.ReconnectAfter;
                opt.Params = original.Params;
                return original;
            }
        }
    }
}
