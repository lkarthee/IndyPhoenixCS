
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