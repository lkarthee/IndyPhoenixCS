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

namespace Indy.Phoenix
{

    public class Response
    {
        readonly static string OK = "ok";
        readonly static string ERROR = "error";
        readonly static string TIMEOUT = "timeout";

        public bool ServerPush;
        public string ChannelId;
        public string RequestId;
        public string Topic;
        public string Event;
        public string Payload;
        public string Status;


        public static Response OkResponse(string Id)
        {
            Response okMessage = new Response
            {
                Event = Id,
                Status = OK,
                Payload = "{}"
            };
            return okMessage;
        }

        public static Response TimeoutResponse()
        {
            Response okMessage = new Response { Status = TIMEOUT, Payload = "{}" };
            return okMessage;
        }

        public Response()
        {
            ChannelId = null;
            RequestId = null;
            Topic = null;
            Event = null;
            Payload = null;
            Status = null;
            ServerPush = false;
        }

        public Response(string raw) : base()
        {
            int start = 1;

            int position = raw.IndexOf(',', start);
            ChannelId = raw.Substring(start, position - start).Trim().Replace("\"", "");
            if (ChannelId == "null")
            {
                ChannelId = null;
            }
            start = position + 1;

            position = raw.IndexOf(',', start);
            RequestId = raw.Substring(start, position - start).Trim().Replace("\"", "");
            if (RequestId == "null")
            {
                ServerPush = true;
                RequestId = null;
            }
            start = position + 1;

            position = raw.IndexOf(',', start);
            Topic = raw.Substring(start, position - start).Trim().Replace("\"", "");
            start = position + 1;

            position = raw.IndexOf(',', start);
            Event = raw.Substring(start, position - start).Trim().Replace("\"", "");
            start = position + 1;

            position = raw.LastIndexOf(']');
            if (position >= 0)
            {
                var rawPayload = raw.Substring(start, position - start).Trim();
                if (ServerPush)
                {
                    Payload = rawPayload;
                }
                else
                {
                    ParseReply(rawPayload);
                }
            }
        }

        private void ParseReply(string raw)
        {
            bool startsWithResponse = raw.StartsWith("{\"response\"");
            if (startsWithResponse)
            {
                int position = raw.IndexOf('{', 1);
                int start = position;
                position = raw.IndexOf('}', position);
                Payload = raw.Substring(start, position - start + 1).Trim();

                if (raw.EndsWith(",\"status\":\"ok\"}"))
                {
                    Status = OK;
                }
                else if (raw.EndsWith(",\"status\":\"error\"}"))
                {
                    Status = ERROR;
                }
                if (raw.EndsWith(",\"status\":\"timeout\"}"))
                {
                    Status = TIMEOUT;
                }
            }
            else
            {
                if (raw.StartsWith("{\"status\":\"ok\""))
                {
                    Status = OK;
                }
                else if (raw.StartsWith("{\"status\":\"error\""))
                {
                    Status = ERROR;
                }
                if (raw.StartsWith("{\"status\":\"timeout\""))
                {
                    Status = TIMEOUT;
                }
                int position = raw.IndexOf('{', 1);
                int start = position + 1;
                position = raw.LastIndexOf('}');
                Payload = raw.Substring(start, position - start).Trim();
            }
        }
    }
}

//public ResponseStatus Status;
//public enum ResponseStatus
//{
//    NotSet,
//    Ok,
//    Error,
//    Timeout,
//}
