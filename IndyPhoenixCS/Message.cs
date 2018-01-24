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

using Newtonsoft.Json.Linq;

namespace Indy.Phoenix

{
    public class Message
    {

        static readonly string Phoenix = "phoenix";
        static readonly string Heartbeat = "heartbeat";
        static readonly JObject StatusOK = JObject.Parse("{status: \"ok\"}");
        static readonly JObject StatusError = JObject.Parse("{status: \"error\"}");
        static readonly JObject StatusTimeout = JObject.Parse("{status: \"timeout\"}");

        public string Topic { get; private set; }
        public string Event { get; private set; }
        public string Ref { get; private set; }
        public string JoinRef { get; private set; }
        public JObject Payload { get; private set; }

        public Message(string topic,string evnt, string reff, string joinRef, JObject payload)
        {
            this.Topic = topic;
            this.Ref = reff;
            this.Payload = payload;
            this.JoinRef = joinRef;
            this.Event = evnt;
        }

        public static Message HeartbeatMessage(string reff)
        {
            return new Message(Phoenix, Heartbeat, reff, null, null);
        }

        public static Message MessageStatusOK(string evnt)
        {
            return new Message(null, evnt, null, null, StatusOK);
        }

        public static Message MessageStatusTimeout(string evnt)
        {
            return new Message(null, evnt, null, null, StatusTimeout);
        }
    }
}