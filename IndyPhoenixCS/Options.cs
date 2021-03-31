// MIT License
//
// Copyright (c) 2018 Kartheek Lenkala
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;

namespace Indy.Phoenix
{
    public class Options
    {
        public TimeSpan? Timeout { get; set; }
        public TimeSpan? HeartbeatInterval { get; set; }
        public Func<int, TimeSpan> ReconnectAfter { get; set; }
        public Func<int, TimeSpan> RejoinChannelAfter { get; set; }
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

            public Builder RejoinChannelAfter(Func<int, TimeSpan> callback)
            {
                opt.RejoinChannelAfter = callback;
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
                opt = new Options
                {
                    Timeout = original.Timeout,
                    HeartbeatInterval = original.HeartbeatInterval,
                    Logger = original.Logger,
                    ReconnectAfter = original.ReconnectAfter,
                    Params = original.Params
                };
                return original;
            }
        }
    }
}
