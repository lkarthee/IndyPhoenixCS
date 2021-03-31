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
using System.Runtime.Serialization;

namespace Indy.Phoenix
{
    public class Channel
    {
        public static readonly string Ok = "ok";
        public static readonly string Error = "error";
        public static readonly string Timeout = "timeout";

        public static readonly string PHX_CLOSE = "phx_close";
        public static readonly string PHX_ERROR = "phx_error";
        public static readonly string PHX_JOIN = "phx_join";
        public static readonly string PHX_REPLY = "phx_reply";
        public static readonly string PHX_LEAVE = "phx_leave";

        public enum ChannelState
        {
            Closed,
            Errored,
            Joined,
            Joining,
            Leaving
        }

        public ChannelState State { get; private set; }

        public bool IsClosed => State == ChannelState.Closed;

        public bool IsErrored => State == ChannelState.Errored;

        public bool IsJoined => State == ChannelState.Joined;

        public bool IsJoining => State == ChannelState.Joining;

        public bool IsLeaving => State == ChannelState.Leaving;

        public bool IsNotClosedOrLeaving => State != ChannelState.Closed || State != ChannelState.Leaving;

        bool joinedOnce;

        public bool CanJoin => !joinedOnce;

        public static readonly List<string> ChannelLifecycleEvents = new List<string>{
            PHX_CLOSE, PHX_ERROR, PHX_JOIN, PHX_REPLY, PHX_LEAVE
        };

        public Socket Socket;

        public string Topic { get; private set; }


        readonly string options;
        readonly List<Binding<Action>> actionBindings;
        readonly List<Binding<Action<string>>> actionStringBindings;
        readonly List<Binding<Action<Response>>> actionResponseBindings;

        readonly List<Push> pushBuffer;
        readonly CallbackTimer rejoinTimer;
        Push joinPush;
        TimeSpan timeout;
        int bindingRef;

        public string Id => joinPush?.Id;

        internal Channel(string topic, Socket socket, string opts)//Dictionary<string, object> opts)//JObject options)
        {
            Socket = socket;
            State = ChannelState.Closed;
            Topic = topic;
            timeout = Socket.Timeout;
            //options = opts;
            options = opts;

            actionBindings = new List<Binding<Action>>();
            actionStringBindings = new List<Binding<Action<string>>>();
            actionResponseBindings = new List<Binding<Action<Response>>>();
            joinedOnce = false;

            pushBuffer = new List<Push>();
            rejoinTimer = new FuncCallbackTimer(RejoinUntilConnected, Socket.RejoinChannelAfter, "channel-rejoin-timer");

            InitJoinPush();
            OnClose(OnChannelClose);
            OnError(OnChannelError);
            On(PHX_REPLY, (Response msg) =>
            {
                Trigger(ReplyEventName(msg.RequestId), msg);//(ReplyEventName(resp.RequestId), msg);
            });
        }

        void OnChannelClose(string code)
        {
            rejoinTimer.Reset();
            Socket.Logger?.Log("channel", "close", "topic " + Topic + ", id " + Id + ", code" + code);
            State = ChannelState.Closed;
            Socket.Remove(this);
        }

        void OnChannelError(string reason)
        {
            if (IsLeaving || IsClosed)
            {
                return;
            }
            Socket.Logger?.Log("channel", string.Format("error {0}", reason));
            State = ChannelState.Errored;
            rejoinTimer.ScheduleTimeout();
        }

        void InitJoinPush()
        {
            joinPush = new Push(this, PHX_JOIN, options, timeout)
                .ReceiveOk(OnJoinReceive)
                .ReceiveError(OnJoinError)
                .ReceiveTimeout(OnJoinTimeout);
        }

        void OnJoinReceive()
        {
            State = ChannelState.Joined;
            rejoinTimer.Reset();
            pushBuffer.ForEach(pushEvent => pushEvent.Send());
            pushBuffer.Clear();
        }

        void OnJoinError(string err)
        {
            State = ChannelState.Errored;
            Socket.Logger?.Log("channel", "error joining topic" + Topic, err);//obj.ToString());
            // START OF ORIGINAL CODE
            //if (socket.IsConnected())
            //{
            //    rejoinTimer.ScheduleTimeout();
            //}
            // END OF ORIGINAL CODE

            // START OF CUSTOMISATION
            var isUnmatchedTopicError = false;

            //TODO :RESTORE
            // TODO : check if the channel join was success. if yes then do
            //if (obj["reason"] != null)
            //{
            //    var reason = obj["reason"].Value<string>();
            //    if (reason == "unmatched topic")
            //    {
            //        isUnmatchedTopicError = true;
            //    }
            //}


            //Socket.Logger?.Log("channel", string.Format("isUnmatchedTopicError = {0}", isUnmatchedTopicError), "");
            //Socket.Logger?.Log("channel", string.Format("rejoinTimer.Tries = {0}", rejoinTimer.Tries), "");
            if (Socket.IsConnected && (!isUnmatchedTopicError || (isUnmatchedTopicError && rejoinTimer.Tries < 2)))
            {
                rejoinTimer.ScheduleTimeout();
            }
            else if ((isUnmatchedTopicError && rejoinTimer.Tries == 2))
            {
                rejoinTimer.Reset();
            }
            //END OF CUSTOMISATION
        }

        void OnJoinTimeout()
        {
            if (!IsJoining)
            {
                return;
            }
            Socket.Logger?.Log("channel", string.Format("timeout {0}", Topic), "");
            Push leavePush = Phoenix.Push.LeavePush(this, timeout);
            leavePush.Send();
            State = ChannelState.Errored;
            joinPush.Reset();
            rejoinTimer.ScheduleTimeout();
        }

        void RejoinUntilConnected()
        {
            rejoinTimer.ScheduleTimeout();
            if (Socket.IsConnected)
            {
                Rejoin(timeout);
            }
        }

        public Push Join(TimeSpan? ts = null)
        {
            if (joinedOnce)
            {
                throw new JoinedOnceException(string.Format("Tried to join {0}  multiple times.", Topic));
            }
            joinedOnce = true;
            Rejoin(ts ?? timeout);
            return joinPush;
        }

        void OnClose(Action<string> callback)
        {
            On(PHX_CLOSE, callback);
        }

        void OnError(Action<string> callback)
        {
            On(PHX_ERROR, (reason) => callback(reason));
        }

        public int On(string refEvent, Action<Response> callback)
        {
            var reff = bindingRef;

            actionResponseBindings.Add(new Binding<Action<Response>>(refEvent, callback));
            bindingRef++;
            return reff;
        }

        public int On(string refEvent, Action<string> callback)
        {
            var reff = bindingRef;
            actionStringBindings.Add(new Binding<Action<string>>(refEvent, callback));
            bindingRef++;
            return reff;
        }

        public int On(string refEvent, Action callback)
        {
            var reff = bindingRef;
            actionBindings.Add(new Binding<Action>(refEvent, callback));
            bindingRef++;
            return reff;
        }

        public void Off(string refEvent, int? reff = null)
        {
            actionBindings.RemoveAll(b => b.Name == refEvent);
            actionStringBindings.RemoveAll(b => b.Name == refEvent);
            actionResponseBindings.RemoveAll(b => b.Name == refEvent);
            //binding.Status != refEvent && ((reff ?? binding.RequestId) == binding.RequestId)});
        }

        public void Trigger(string eventName)
        {

        }


        public void Trigger(string eventName, Response msg)
        {
            Socket.Logger.Log("channel", "trigger", "event =" + eventName);
            var ab = actionBindings.FindAll(b => b.IsMatch(eventName));
            ab.ForEach(b => b.Callback.Invoke());

            var arb = actionResponseBindings.FindAll(b => b.IsMatch(eventName));
            arb.ForEach(b => b.Callback.Invoke(msg));

            var asb = actionStringBindings.FindAll(b => b.IsMatch(eventName));
            asb.ForEach(b => b.Callback?.Invoke(msg?.Payload));
        }

        bool CanPush => Socket.IsConnected && IsJoined;

        public Push Push(string evnt)
        {
            return Push(evnt, null, timeout);
        }

        public Push Push(string evnt, string payload)
        {
            return Push(evnt, payload, timeout);
        }

        public Push Push(string evnt, string payload, TimeSpan timeout)
        {
            if (!joinedOnce)
            {
                throw new Exception("Tried to push " + evnt + " to " + Topic +
                                    " before joining. Use Channel.Join() before pushing events");
            }
            Push pushEvent = new Push(this, evnt, payload, timeout);
            if (CanPush)
            {
                pushEvent.Send();
            }
            else
            {
                pushEvent.StartTimeout();
                pushBuffer.Add(pushEvent);
            }

            return pushEvent;
        }

        public Push Leave()
        {
            return Leave(timeout);
        }

        public Push Leave(TimeSpan timeout)
        {
            State = ChannelState.Leaving;
            void onClose()
            {
                Socket.Logger?.Log("channel", "leave " + Topic);
                Trigger(PHX_CLOSE);

            }

            Push leavePush = new Push(this, PHX_LEAVE, null, timeout);
            leavePush.ReceiveOk(onClose)
                     .ReceiveTimeout(onClose);
            leavePush.Send();
            if (!CanPush)
            {
                leavePush.TriggerTimeout();

            }
            return leavePush;
        }

        bool IsLifecycleEvent(Response msg)
        {
            return ChannelLifecycleEvents.Contains(msg.Event);
        }

        internal bool IsMember(Response msg)
        {
            if (Topic != msg.Topic)
            {
                return false;
            }
            if (msg.ChannelId != null && IsLifecycleEvent(msg) && msg.ChannelId != Id)
            {
                return false;
            }
            return true;
        }

        void Rejoin(TimeSpan? ts = null)
        {
            if (IsLeaving)
            {
                return;
            }
            SendJoin(ts ?? timeout);
        }

        void SendJoin(TimeSpan? ts = null)
        {
            State = ChannelState.Joining;
            joinPush.Resend(ts ?? timeout);
        }

        internal string ReplyEventName(string id)
        {
            return "chan_reply_" + id;
        }

        [Serializable]
        private class JoinedOnceException : Exception
        {
            public JoinedOnceException()
            {
            }

            public JoinedOnceException(string message) : base(message)
            {
            }

            public JoinedOnceException(string message, Exception innerException) : base(message, innerException)
            {
            }

            protected JoinedOnceException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        [Serializable]
        private class CallbackNotInvokedException : Exception
        {
            public CallbackNotInvokedException()
            {
            }

            public CallbackNotInvokedException(string message) : base(message)
            {
            }

            public CallbackNotInvokedException(string message, Exception innerException) : base(message, innerException)
            {
            }

            protected CallbackNotInvokedException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        [Serializable]
        private class EmptyOnMessageException : Exception
        {
            public EmptyOnMessageException()
            {
            }

            public EmptyOnMessageException(string message) : base(message)
            {
            }

            public EmptyOnMessageException(string message, Exception innerException) : base(message, innerException)
            {
            }

            protected EmptyOnMessageException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
}
