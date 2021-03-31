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
using System.Text;

namespace Indy.Phoenix
{
    public class Push
    {
        readonly static string OK = "ok";
        readonly static string ERROR = "error";
        readonly static string TIMEOUT = "timeout";

        public string Id;
        public Channel Channel { get; private set; }
        public string Event;
        public string ResponseId;
        readonly string payload;
        //private bool sent;
        TimeSpan timeout;
        CallbackTimer timeoutTimer;

        readonly List<Binding<Action>> actionBindings;
        readonly List<Binding<Action<string>>> actionStringBindings;

        Action timeoutCallback;
        Response Response;

        public static Push LeavePush(Channel channel, TimeSpan timeout)
        {
            return new Push(channel, Channel.PHX_LEAVE, null, timeout);
        }

        public Push(Channel channel, string evnt, string payload, TimeSpan timeout)
        {
            Id = null;
            Channel = channel;
            Event = evnt;
            this.payload = payload;
            this.timeout = timeout;
            timeoutTimer = null;
            actionBindings = new List<Binding<Action>>();
            actionStringBindings = new List<Binding<Action<string>>>();
            //sent = false;
        }

        public void Resend(TimeSpan timeout)
        {
            this.timeout = timeout;
            Reset();
            Send();
        }

        public void Send()
        {
            //sent = true;
            StartTimeout();
            //Message msg = new Message(Channel.Topic, Event,
            //                          Id, Channel.Id, payload);
            StringBuilder sb = new StringBuilder();
            sb.Append("[\"").Append(Channel.Id).Append("\",");
            sb.Append("\"").Append(Id).Append("\",\"");
            sb.Append(Channel.Topic).Append("\",\"").Append(Event).Append("\",");
            if (payload == null)
            {
                sb.Append("{}]");
            }
            else
            {
                sb.Append(payload).Append("]");
            }
            Channel.Socket.Push(sb.ToString());
        }


        public Push ReceiveOk(Action callback) => Receive(OK, callback);
        public Push ReceiveOk(Action<string> callback) => Receive(OK, callback);

        public Push ReceiveError(Action callback) => Receive(ERROR, callback);
        public Push ReceiveError(Action<string> callback) => Receive(ERROR, callback);

        public Push ReceiveTimeout(Action callback) => Receive(TIMEOUT, callback);

        private Push Receive(string status, Action callback)
        {
            if (HasReceived(status))
            {
                callback?.Invoke();
            }
            var binding = new Binding<Action>(status, callback);
            actionBindings.Add(binding);
            return this;
        }

        //if (status == ResponseStatus.Ok)
        //{
        //    okCallback = callback;
        //}
        //else if (status == ResponseStatus.Error)
        //{
        //    errCallback = callback;
        //}
        //else if (status == ResponseStatus.Timeout)
        //{
        //    timeoutCallback = callback;
        //}

        private Push Receive(string status, Action<string> callback)
        {
            if (HasReceived(status))
            {
                callback(Response.Payload);
            }
            var binding = new Binding<Action<string>>(status, callback);
            actionStringBindings.Add(binding);
            return this;
        }


        void CancelRefEvent()
        {
            if (Id == null)
            {
                return;
            }
            Channel.Off(ResponseId);
        }

        internal void StartTimeout()
        {
            if (timeoutTimer == null)
            {
                CancelTimeout();
            }

            Id = Channel.Socket.MakeRef();
            ResponseId = Channel.ReplyEventName(Id);
            Channel.On(ResponseId, OnResponse);

            timeoutTimer = new FixedCallbackTimer(OnTimeout, timeout, "push-timer-" + Id);
        }

        void OnTimeout()
        {
            TriggerTimeout();
        }

        void OnResponse(Response msg)
        {
            CancelRefEvent();
            CancelTimeout();
            Response = msg;
            MatchReceive(msg);
        }

        internal void Reset()
        {
            Id = null;
            CancelRefEvent();
            ResponseId = null;
            //receivedResponse = null;
            //this.sent = false;
        }

        void MatchReceive(Response msg)
        {
            var ab = actionBindings.FindAll(b => b.IsMatch(msg.Status));
            ab.ForEach(b => b.Callback.Invoke());
            var asb = actionStringBindings.FindAll(b => b.IsMatch(msg.Status));
            asb.ForEach(b => b.Callback?.Invoke(msg?.Payload));
        }


        void CancelTimeout()
        {
            if (timeoutTimer != null)
            {
                timeoutTimer.Reset();
            }

            timeoutTimer = null;
        }

        bool HasReceived(string status)
        {
            if (Response.Status == null)
            {
                return false;
            }
            return Response.Status == status;
        }

        public void TriggerTimeout()
        {
            var msg = Response.TimeoutResponse();
            Trigger(msg);
        }

        public void Trigger(Response msg)
        {
            Channel.Trigger(ResponseId, msg);
        }
    }
    //if (status == ResponseStatus.Ok)
    //{
    //    okCallbackString = callback;
    //}
    //else if (status == ResponseStatus.Error)
    //{
    //    errCallbackString = callback;
    //}

    //var recHooks = bindings.FindAll(rechook => rechook.Name == ResponseStatus);
    //recHooks.ForEach((rechook) => rechook.Invoke(response["response"].Value<JObject>()));
    //TODO: enable rechooks
    //throw new System.NotImplementedException();

    //sb.Append("[");
    //if (Channel.Id == null)
    //{
    //    sb.Append("[null,");
    //}
    //else
    //{
    //sb.Append("[\"").Append(Channel.Id).Append("\",");
    //}

    //if (Id == null)
    //{
    // sb.Append("null,\"");
    //}
    //else
    //{
    //sb.Append("\"").Append(Id).Append("\",\"");
    //}

    //string replyEventName;

    //internal void Trigger(string evnt, Message message)
    //{
    //    Channel.Trigger(evnt, message);
    //}

    //internal void Trigger(Message response)
    //{
    //    Channel.Trigger(@event, response);
    //}


    //if (HasReceived(ResponseStatus.Timeout)) //"timeout"))
    //{
    //    return;
    //}

    //bindings.Add(new Binding(status, callback));
    //bindings.Add(new Binding<ResponseStatus, string>(status, callback));

    //public Push<T> ReceiveOk(Action<T> callback)
    //{
    //    if (HasReceived(status))
    //    {
    //        //callback?.Invoke(Response.);
    //    }


    //    return this;
    //}
    //bindings.Add(new Binding(status, callback));




    //public Push ReceiveTimeout(Action<string> callback) => Receive(ResponseStatus.Timeout, callback);

    //public Push ReceiveTimeout(Action<byte[]> callback) => Receive(Channel.Timeout, callback);
    //public Push ReceiveError(Action<byte[]> callback) => Receive(Channel.Error, callback);

    //public Push Receive(string status, Action<byte[]> callback)
    //{
    //    if (HasReceived(status))
    //    {
    //        //callback(receivedResponse["response"].Value<byte[]>());
    //        callback((byte[])receivedResponse);
    //    }

    //    bindings.Add(new Binding(status, callback));
    //    return this;
    //}

    //public Push ReceiveOk(Action<byte[]> callback) => Receive(Channel.Ok, callback);

}

//JObject receivedResponse;
//string respStatus;


//readonly JObject payload;

//public Push(Channel channel, string evnt, JObject payload, TimeSpan timeout)
//{
//    Channel = channel;
//    Event = evnt;
//    this.payload = payload;
//    this.timeout = timeout;
//    receivedResponse = null;
//    timeoutTimer = null;
//    bindings = new List<Binding>();
//}


//Channel.On(refEvent, (resp) =>
//{
//CancelRefEvent();
//CancelTimeout();
//receivedResponse = msg;
//MatchReceive(msg);
//throw new System.NotImplementedException();
//});

//public Push Receive(string status, Action<JObject> callback)
//{
//    if (HasReceived(status))
//    {
//        callback(receivedResponse["response"].Value<JObject>());
//    }

//    bindings.Add(new Binding(status, callback));
//    return this;
//}

//public Push ReceiveOk(Action<JObject> callback) => Receive(Channel.Ok, callback);

//string ResponseStatus;
//{
//    get
//    {
//        if (respStatus == null && receivedResponse != null)
//        {
//            respStatus = receivedResponse["status"].Value<string>();
//        }
//        return respStatus;
//    }
//}
//public Push ReceiveError(Action<JObject> callback) => Receive(Channel.Error, callback);
//public Push ReceiveTimeout(Action<JObject> callback) => Receive(Channel.Timeout, callback);

//readonly List<Binding<ResponseStatus, Action>> actionBindings;
//readonly List<Binding<ResponseStatus, Action<string>>> actionStringBindings;
//string @event;

//Response = new ResponseMessage();
//actionBindings = new List<Binding<ResponseStatus, Action>>();
//actionStringBindings = new List<Binding<ResponseStatus, Action<string>>>();
//ResponseStatus = null;

//Action okCallback;
//Action<string> okCallbackString;
//Action errCallback;
//Action<string> errCallbackString;
//Action timeoutCallback;
//Action errCallback;
//Action<string> errCallbackString;


// if (msg.Status == ResponseStatus.Ok)
//{
//    okCallback?.Invoke();
//    okCallbackString?.Invoke(msg.Payload);
//}
//else if (msg.Status == ResponseStatus.Error)
//{
//    errCallback?.Invoke();
//    errCallbackString?.Invoke(msg.Payload);
//}
//else if (msg.Status == ResponseStatus.Timeout)
//{
//    timeoutCallback?.Invoke();
//}
//else
//{
//    throw new Exception("response status is not set.");
//}