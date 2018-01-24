using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Indy.Phoenix
{
    public class Push
    {
        public Channel channel { get; }
        public string sEvent;
        JObject payload;
        TimeSpan timeout;
        CallbackTimer timeoutTimer;
        List<Binding> bindings;
        JObject receivedResponse;

        public string Ref;
        string refEvent;

        public Push(Channel channel, string evnt, JObject payload, TimeSpan timeout)
        {
            this.channel = channel;
            this.sEvent = evnt;
            this.payload = payload;
            this.timeout = timeout;
            this.receivedResponse = null;
            this.timeoutTimer = null;
            this.bindings = new List<Binding>();
        }

        public void Resend(TimeSpan timeout)
        {
            this.timeout = timeout;
            this.Reset();
            this.Send();
        }

        public void Send()
        {
            if (this.HasReceived("timeout"))
            {
                return;
            }
            this.StartTimeout();
            Message msg = new Message(this.channel.topic, this.sEvent, 
                                      this.Ref, this.channel.JoinRef(), this.payload);
            this.channel.Socket.Push(msg);
        }

        public Push Receive(string status, Action callback)
        {
            if (HasReceived(status))
            {
                callback();
            }

            bindings.Add(new Binding(status, callback));
            return this;
        }

        public Push Receive(string status, Action<JObject> callback)
        {
            if (HasReceived(status))
            {
                callback(receivedResponse["response"].Value<JObject>());
            }

            bindings.Add(new Binding(status,  callback));
            return this;
        }

        void CancelRefEvent()
        {
            if (this.refEvent == null)
            {
                return;
            }
        }

        internal void StartTimeout()
        {
            if (this.timeoutTimer == null)
            {
                this.CancelTimeout();
            }

            this.Ref = this.channel.Socket.MakeRef();
            this.refEvent = this.channel.ReplyEventName(this.Ref);
            this.channel.On(this.refEvent, (resp) =>
            {
                this.CancelRefEvent();
                this.CancelTimeout();
                this.receivedResponse = resp;
                this.MatchReceive(resp);
            });
            this.timeoutTimer = new CallbackTimer(() => { 
                this.Trigger(this.refEvent, Message.MessageStatusTimeout(this.refEvent)); 
            }, timeout);
        }

        internal void Reset()
        {
            this.Ref = null;
            this.CancelRefEvent();
            this.refEvent = null;
            this.receivedResponse = null;
            //this.sent = false;
        }

        void MatchReceive(JObject response) {
            var recHooks = this.bindings.FindAll(rechook => rechook.Name == ResponseStatus);
            recHooks.ForEach((rechook) => rechook.Invoke(response["response"].Value<JObject>()));
        }

        void CancelTimeout()
        {
            if (timeoutTimer != null)
            {
                this.timeoutTimer.Reset();    
            }

            this.timeoutTimer = null;
        }

        bool HasReceived(string status)
        {
            if (this.receivedResponse == null)
            {
                return false;
            }
            return ResponseStatus == status;
        }

        string respStatus;

        string ResponseStatus
        {
            get
            {
                if (respStatus == null && this.receivedResponse != null)
                {
                    respStatus = this.receivedResponse["status"].Value<string>();
                }
                return respStatus;
            }
        }

        internal void Trigger(string evnt, Message message)
        {
            this.channel.Trigger(evnt, message);
        }

        internal void Trigger(Message response)
        {
            this.channel.Trigger(this.refEvent, response);
        }

    }
}
