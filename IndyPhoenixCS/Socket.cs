using System;
using System.Text;
using System.Collections.Generic;

using WebSocketSharp;

using Newtonsoft.Json.Linq;


namespace Indy.Phoenix
{

    public class Socket
    {


        public enum State
        {
            Connecting,
            Open,
            Closing,
            Closed
        }

        static readonly string VSN_STRING = "/websocket?vsn=2.0.0";

        List<Action> openCallbacks;
        List<Action<ushort, string>> closeCallbacks;
        List<Action<string>> errorCallbacks;
        List<Action<string>> messageCallbacks;

        const int DefaultTimeout = 10000;
        const int WebSocketCloseNormal = 1000;
        List<Channel> channels;

        WebSocket Connection;
        ILogger Logger;

        int Ref;
        TimeSpan HeartbeatInterval;
        public Func<int, TimeSpan> ReconnectAfter { get; }
        public TimeSpan Timeout;
        Options options;

        CallbackTimer HeartBeatTimer;
        string pendingHeartBeatRef;

        CallbackTimer ReconnectTimer;

        string Endpoint;
        readonly List<Action> SendBuffer;



        public Socket(String endpoint, Options options = null)
        {
            SendBuffer = new List<Action>();
            channels = new List<Channel>();

            openCallbacks = new List<Action>();
            closeCallbacks = new List<Action<ushort, string>>();
            errorCallbacks = new List<Action<string>>();
            messageCallbacks = new List<Action<string>>();

            this.Endpoint = endpoint + VSN_STRING;

            Ref = 0;

            this.options = options;

            if (options != null && options.Timeout != null)
            {
                this.Timeout = (TimeSpan)options.Timeout;
            }
            else
            {
                this.Timeout = TimeSpan.FromMilliseconds(DefaultTimeout);
            }

            if (options != null && options.Logger != null)
            {
                this.Logger = options.Logger;
            }
            else
            {
                this.Logger = new Logger();
            }
            //TODO: set default encoder / decoder
            //TODO: set encoder/decoder
            if (options != null && options.HeartbeatInterval != null)
            {
                this.HeartbeatInterval = (TimeSpan)options.HeartbeatInterval;
            }
            else
            {
                this.HeartbeatInterval = TimeSpan.FromMilliseconds(30000);
            }
            if (options != null && options.ReconnectAfter != null)
            {
                this.ReconnectAfter = options.ReconnectAfter;
            }
            else
            {
                this.ReconnectAfter = ReconnectAfterDefault;
            }

            this.HeartBeatTimer = null;
            this.pendingHeartBeatRef = null;

            this.ReconnectTimer = new CallbackTimer(
                () => { this.Disconnect(() => this.Connect()); },
                this.ReconnectAfter
            );


        }

        int[] backOffTimes = { 1000, 2000, 5000, 10000 };

        TimeSpan ReconnectAfterDefault(int tries)
        {
            if (tries > 0 && tries < backOffTimes.Length)
            {
                return TimeSpan.FromMilliseconds(backOffTimes[tries - 1]);
            }
            return TimeSpan.FromMilliseconds(10000);
        }

        public void Disconnect(Action callback, int? code = null, string reason = null)
        {
            if (this.Connection != null)
            {
                //this.Connection.OnClose =
                if (code != null)
                {
                    this.Connection.Close((ushort)code,
                                          reason ?? string.Empty);
                }
                else
                {
                    this.Connection.Close();
                }
                this.Connection = null;
            }

            if (callback != null)
            {
                callback.Invoke();
            }
        }

        internal string BuildParams()
        {
            if (options.Params == null)
            {
                return string.Empty;
            }
            StringBuilder retValue = new StringBuilder("");
            foreach (KeyValuePair<string, string> item in options.Params)
            {
                retValue.Append("&");
                retValue.Append(item.Key);
                retValue.Append("=");
                retValue.Append(item.Value);
            }
            return retValue.ToString();
        }

        public void Connect()
        {
            if (Connection != null)
            {
                return;
            }
            //UnityEngine.Debug.Log("Connecting to Host: " + Endpoint + BuildParams());
            Connection = new WebSocket(Endpoint + BuildParams());
            Connection.WaitTime = Timeout;
            Connection.OnOpen += OnConnectionOpen;
            Connection.OnError += OnConnectionError;
            Connection.OnClose += OnConnectionClose;
            Connection.OnMessage += OnConnectionMessage;
            Connection.Connect();
        }

        internal void Log(string kind, string msg, object data = null)
        {
            if (Logger == null)
            {
                return;
            }

            if (data != null)
            {
                Logger.Log(kind + " " + msg + "" + data.ToString());
            }
        }

        public void OnOpen(Action callback)
        {
            this.openCallbacks.Add(callback);
        }

        public void OnClose(Action<ushort, string> callback)
        {
            this.closeCallbacks.Add(callback);
        }

        public void OnError(Action<string> callback)
        {
            this.errorCallbacks.Add(callback);
        }

        public void OnMessage(Action<string> callback)
        {
            this.messageCallbacks.Add(callback);
        }

        void OnConnectionMessage(object sender, MessageEventArgs args)
        {
            Logger.Log("OnConnectionMessage - " + args.Data);
            this.Decode(args.Data, message =>
            {
                if (message.Ref != null && message.Ref == this.pendingHeartBeatRef)
                {
                    this.pendingHeartBeatRef = null;
                }
                this.Log("receive", 
                        string.Format("{0} {1} {2}", message.Topic, message.Event, 
                                      message.Ref == null ? "" : message.Ref));
                var memberChannels = channels.FindAll(
                    channel => channel.IsMember(message));

                memberChannels.ForEach(channel => channel.Trigger(message.Event, message));
            });
        }

        void OnConnectionOpen(object sender, EventArgs args)
        {
            Log("websocket", "open");
            FlushSendBuffer();
            ReconnectTimer.Reset();
            if (HeartBeatTimer == null)
            {
                HeartBeatTimer = new CallbackTimer(SendHeartBuffer, this.HeartbeatInterval);
            }
            else
            {
                this.HeartBeatTimer.Reset();
            }
            this.HeartBeatTimer.ScheduleTimeout();
            this.openCallbacks.ForEach(callback => callback());
        }

        void OnConnectionClose(object sender, CloseEventArgs args)
        {
            Log("websocket", "close ", " code - " + args.Code.ToString() + " reason - " + args.Reason);
            TriggerChannelError();
            if (HeartBeatTimer !=null ) {
                HeartBeatTimer.Reset();   
            }
            ReconnectTimer.ScheduleTimeout();
            closeCallbacks.ForEach(callback => callback(args.Code, args.Reason));
        }

        void OnConnectionError(object sender, ErrorEventArgs args)
        {
            Log("websocket", args.Message);
            TriggerChannelError();
            errorCallbacks.ForEach(callback => callback(args.Message));
        }

        void TriggerChannelError()
        {
            channels.ForEach(
                (channel) => channel.Trigger(Channel.PHX_ERROR, null)
            );
        }

        public State ConnectionState()
        {
            if (Connection == null)
            {
                return State.Closed;
            }
            switch (Connection.ReadyState)
            {
                case WebSocketState.Connecting:
                    return State.Connecting;
                case WebSocketState.Open:
                    return State.Open;
                case WebSocketState.Closing:
                    return State.Closing;
                default:
                    return State.Closed;
            }
        }

        public bool IsConnected()
        {
            return ConnectionState() == State.Open;
        }

        public void Remove(Channel channel)
        {
            this.channels.RemoveAll(c => channel.JoinRef() == c.JoinRef());
        }

        public Channel GetChannel(string topic, JObject options)
        {
            Channel channel = new Channel(topic, this, options);
            channels.Add(channel);
            return channel;
        }

        public void Push(Message msg)
        {
            Action callback = () =>
            {
                this.Encode(msg, result =>
                {
                    this.Logger.Log("Message - " + result);
                    this.Connection.Send(result);
                });
            };

            if (IsConnected())
            {
                callback.Invoke();
            }
            else
            {
                SendBuffer.Add(callback);
            }
        }

        void Encode(Message msg, Action<string> callback)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");

            if (msg.JoinRef == null)
            {
                sb.Append("null,");
            }
            else
            {
                sb.Append("\"").Append(msg.JoinRef).Append("\",");
            }

            if (msg.Ref == null)
            {
                sb.Append("null,\"");
            }
            else
            {
                sb.Append("\"").Append(msg.Ref).Append("\",\"");
            }

            sb.Append(msg.Topic).Append("\",\"").Append(msg.Event).Append("\",");

            if (msg.Payload == null)
            {
                sb.Append("{}]");
            }
            else
            {
                sb.Append(msg.Payload.ToString()).Append("]");
            }
            callback(sb.ToString());
        }

        void Decode(string rawPayload, Action<Message> callback)
        {
            var arr = JArray.Parse(rawPayload);
            var evnt = arr[3] == null ? null : arr[3].ToString();
            var topic = arr[2] == null ? null : arr[2].ToString();
            var reff = arr[1] == null ? null : arr[1].ToString();
            var joinRef = arr[0] == null ? null : arr[0].ToString();
            var payload = arr[4].Value<JObject>();
            var message = new Message(topic,evnt, reff, joinRef, payload);
            callback(message);
        }

        public string MakeRef()
        {
            Ref++;
            return (Ref).ToString();
        }

        void SendHeartBuffer()
        {
            if (!IsConnected())
            {
                return;
            }
            if (pendingHeartBeatRef != null)
            {
                this.pendingHeartBeatRef = null;
                this.Log("transport", "heartbeat timeout. Attempting to re-establish connection");
                this.Connection.Close(WebSocketCloseNormal, "heartbeat timeout");
                return;
            }
            this.pendingHeartBeatRef = this.MakeRef();
            this.Push(Message.HeartbeatMessage(this.pendingHeartBeatRef));
        }

        void FlushSendBuffer()
        {
            if (IsConnected() && SendBuffer.Count > 0)
            {
                this.SendBuffer.ForEach(callback => callback());
                this.SendBuffer.Clear();
            }
        }
    }
}