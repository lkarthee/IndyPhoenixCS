using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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

        public enum State
        {
            Closed,
            Errored,
            Joined,
            Joining,
            Leaving
        }

        public static readonly List<string> ChannelLifecycleEvents = new List<string>{
            PHX_CLOSE, PHX_ERROR, PHX_JOIN, PHX_REPLY, PHX_LEAVE
        };

        public Socket Socket;
        State state;
        public string topic { get; }
        JObject options;
        List<Binding> bindings;
        bool joinedOnce;
        List<Push> pushBuffer;
        CallbackTimer rejoinTimer;
        Push joinPush;
        TimeSpan timeout;
        int bindingRef;


        internal Channel(string topic, Socket socket, JObject options)
        {
            this.Socket = socket;
            this.state = State.Closed;
            this.topic = topic;
            this.timeout = this.Socket.Timeout;
            this.options = options;
            this.bindings = new List<Binding>();
            this.joinedOnce = false;
            this.joinPush = new Push(this, PHX_JOIN, this.options, this.timeout);
            this.pushBuffer = new List<Push>();
            this.rejoinTimer = new CallbackTimer(() => { this.RejoinUntilConnected(); }, this.Socket.ReconnectAfter);
            this.joinPush.Receive(Channel.Ok, () =>
            {
                this.state = Channel.State.Joined;
                this.rejoinTimer.Reset();
                this.pushBuffer.ForEach(pushEvent => pushEvent.Send());
                this.pushBuffer.Clear();
            });
            this.OnClose((input) =>
            {
                this.rejoinTimer.Reset();
                this.Socket.Log("channel", String.Format("close {0} {1}", topic, JoinRef()));
                this.state = Channel.State.Closed;
                this.Socket.Remove(this);
            });

            this.OnError(reason =>
            {
                if (this.IsLeaving() || this.IsClosed())
                {
                    return;
                }
                this.Socket.Log("channel", String.Format("error {0}", reason));
                this.state = State.Errored;
                this.rejoinTimer.ScheduleTimeout();
            });

            this.joinPush.Receive(Channel.Timeout, () =>
            {
                if (!this.IsJoining()) return;
                this.Socket.Log("channel", "timeout", "");
                var leavePush = new Push(this, PHX_LEAVE, null, timeout);
                leavePush.Send();
                this.state = State.Errored;
                this.joinPush.Reset();
                this.rejoinTimer.ScheduleTimeout();
            });

            this.On(PHX_REPLY, (resp) =>
            {
                this.Trigger(this.ReplyEventName(resp.Ref), resp);
            });
        }

        void RejoinUntilConnected()
        {
            this.rejoinTimer.ScheduleTimeout();
            if (this.Socket.IsConnected())
            {
                this.Rejoin(this.timeout);
            }
        }

        public Push Join(TimeSpan? ts = null)
        {
            if (this.joinedOnce)
            {
                throw new Exception("Tried to join multiple times.");
            }

            this.joinedOnce = true;
            this.Rejoin(ts ?? timeout);
            return this.joinPush;

        }

        void OnClose(Action<JObject> callback)
        {
            this.On(PHX_CLOSE, callback);
        }

        void OnError(Action<JObject> callback)
        {
            this.On(PHX_ERROR, (reason) => callback(reason));
        }

        int On(string refEvent, Action<Message> callback)
        {
            var reff = bindingRef;
            this.bindings.Add(new Binding(refEvent, bindingRef, callback));
            bindingRef++;
            return reff;
        }

        public int On(string refEvent, Action<JObject> callback)
        {
            var reff = bindingRef;
            this.bindings.Add(new Binding(refEvent, bindingRef, callback));
            bindingRef++;
            return reff;
        }

        public void Off(string refEvent, int? reff = null)
        {
            this.bindings = this.bindings.FindAll((binding) => binding.Name != refEvent && ((reff ?? binding.Ref) == binding.Ref));
        }

        bool CanPush()
        {
            return this.Socket.IsConnected() && this.IsJoined();
        }

        public Push Push(string evnt)
        {
            return Push(evnt, null, this.timeout);
        }

        public Push Push(string evnt, JObject payload)
        {
            return Push(evnt, payload, this.timeout);
        }

        public Push Push(string evt, JObject payload, TimeSpan timeout)
        {
            if (!this.joinedOnce)
            {
                throw new Exception("Tried to push " + evt + " to " + this.topic +
                                    " before joining. Use Channel.Join() before pushing events");
            }
            Push pushEvent = new Push(this, evt, payload, timeout);
            if (CanPush())
            {
                pushEvent.Send();
            }
            else
            {
                pushEvent.StartTimeout();
                this.pushBuffer.Add(pushEvent);
            }

            return pushEvent;
        }

        public Push Leave(TimeSpan timeout)
        {
            this.state = Channel.State.Leaving;
            Action onClose = () =>
            {
                this.Socket.Log("channel", "leave " + this.topic);
                this.Trigger(PHX_CLOSE, null);
            };
            var leaveEvent = PHX_LEAVE;//ChannelEventString[Channel.Event.Leave];
            Push leavePush = new Push(this, leaveEvent, null, timeout);
            leavePush.Receive(Channel.Ok, onClose)
                     .Receive(Channel.Timeout, onClose);
            leavePush.Send();
            if (!this.CanPush())
            {
                leavePush.Trigger(leaveEvent, Message.MessageStatusOK(leaveEvent));
            }
            return leavePush;
        }

        public JObject OnMessage(string evnt, Message response)
        {
            return response == null ? null : response.Payload;
        }

        internal bool IsMember(Message message)
        {
            if (topic != message.Topic)
            {
                return false;
            }
            if (message.JoinRef != null && ChannelLifecycleEvents.Contains(message.Event) && message.JoinRef != this.JoinRef())
            {
                return false;
            }
            return true;
        }

        internal string JoinRef()
        {
            return joinPush.Ref;
        }

        void Rejoin(TimeSpan? ts = null)
        {
            if (this.IsLeaving())
            {
                return;
            }
            this.SendJoin(ts ?? timeout);
        }

        void SendJoin(TimeSpan? ts = null)
        {
            this.state = Channel.State.Joining;
            this.joinPush.Resend(ts ?? timeout);
        }

        internal void Trigger(string eventName, Message response)
        {
            var handledPayload = this.OnMessage(eventName, response);
            if (response != null && response.Payload != null && handledPayload == null)
            {
                throw new Exception("channel onMessage callback must return the payload, modified or unmodified");
            }

            var filteredBindings = this.bindings.FindAll(bind => { return bind.Name == eventName ; });
            filteredBindings.ForEach(binding =>
            {
                if (binding.Params == Binding.ParamType.NoParams)
                {
                    binding.Invoke();
                    } else if(binding.Params == Binding.ParamType.Message){
                    binding.InvokeMsg(response);
                    } else if (binding.Params == Binding.ParamType.JObject)
                {
                    binding.InvokeJObj(handledPayload);
                }
                else {
                    throw new Exception("Callback not invoked - check binding type(added new binding type?)");
                }
            });
        }

        internal string ReplyEventName(string reff)
        {
            return "chan_reply_" + reff;
        }

        bool IsClosed()
        {
            return this.state == Channel.State.Closed;
        }

        bool IsErrored()
        {
            return this.state == Channel.State.Errored;
        }

        bool IsJoined()
        {
            return this.state == Channel.State.Joined;
        }

        bool IsJoining()
        {
            return this.state == Channel.State.Joining;
        }

        bool IsLeaving()
        {
            return this.state == Channel.State.Leaving;
        }
    }
}