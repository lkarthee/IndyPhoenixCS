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
using System.Threading.Tasks;
using NativeWebSocket;

namespace Indy.Phoenix
{
    public delegate void SocketOpenEventHandler();
    public delegate void SocketCloseEventHandler(int code);
    public delegate void SocketErrorEventHandler(string msg);

    public class Socket
    {
        public enum SocketState
        {
            Connecting,
            Open,
            Closing,
            Closed
        }

        static readonly string VSN_STRING = "/websocket?vsn=2.0.0";


        public event SocketOpenEventHandler OnOpen;
        public event SocketCloseEventHandler OnClose;
        public event SocketErrorEventHandler OnError;

        const int DefaultTimeout = 10000;
        private static string SOCKET_TXT = "socket";

        readonly List<Channel> channels;

        UnityWebSocket websocket;
        public readonly ILogger Logger;

        int Ref;
        readonly TimeSpan HeartbeatInterval;
        public Func<int, TimeSpan> ReconnectAfter { get; private set; }
        public Func<int, TimeSpan> RejoinChannelAfter { get; private set; }
        public TimeSpan Timeout;
        readonly Options options;

        FixedCallbackTimer HeartBeatTimer;
        string pendingHeartBeatRef;

        FuncCallbackTimer SocketReconnectTimer;
        readonly string Endpoint;
        readonly List<Action> SendBuffer;

        public Socket(string endpoint, Options opts = null)
        {
            SendBuffer = new List<Action>();
            channels = new List<Channel>();
            Endpoint = endpoint + VSN_STRING;
            Ref = 0;
            options = opts;
            Timeout = opts != null && opts.Timeout != null ? (TimeSpan)opts.Timeout : TimeSpan.FromMilliseconds(DefaultTimeout);
            Logger = opts?.Logger;
            //TODO: set default encoder / decoder
            //TODO: set encoder/decoder
            HeartbeatInterval = opts != null && opts.HeartbeatInterval != null ? (TimeSpan)opts.HeartbeatInterval : TimeSpan.FromMilliseconds(30000);
            RejoinChannelAfter = opts != null && opts.RejoinChannelAfter != null ? opts.ReconnectAfter : RejoinChannelAfterDefault;
            ReconnectAfter = opts != null && opts.ReconnectAfter != null ? opts.ReconnectAfter : ReconnectAfterDefault;
            SocketReconnectTimer = new FuncCallbackTimer(Reconnect, ReconnectAfter, true, true, "socket-reconnect-timer");
        }

        async void Reconnect()
        {
            Logger?.Log(SOCKET_TXT, "reconnect");
            await DisconnectAsync().ContinueWith((t) => { _ = ConnectAsync(); });
        }

        static readonly int[] rejoinChannelBackOffTimes = { 1000, 2000, 5000 };
        static readonly int[] reconnectBackOffTimes = { 10, 50, 100, 150, 200, 250, 500, 1000, 2000 };

        TimeSpan RejoinChannelAfterDefault(int tries)
        {
            Logger?.Log("channel", "rejoin default tries" + tries);
            if (tries > 0 && tries < rejoinChannelBackOffTimes.Length)
            {
                var backOff = rejoinChannelBackOffTimes[tries - 1];
                Logger?.Log("channel", "rejoin default backoff" + backOff);
                return TimeSpan.FromMilliseconds(backOff);
            }
            Logger?.Log("channel", "rejoin channel default backoff" + 10000);
            return TimeSpan.FromMilliseconds(10000);
        }


        TimeSpan ReconnectAfterDefault(int tries)
        {
            Logger?.Log(SOCKET_TXT, " reconnect default tries" + tries);
            if (tries > 0 && tries < reconnectBackOffTimes.Length)
            {
                var backOff = reconnectBackOffTimes[tries - 1];
                Logger?.Log(SOCKET_TXT, " reconnect default backoff" + backOff);
                return TimeSpan.FromMilliseconds(backOff);
            }
            Logger?.Log(SOCKET_TXT, " reconnect default backoff" + 5000);
            return TimeSpan.FromMilliseconds(5000);
        }

        public async Task DisconnectAsync(Action callback)
        {
            Logger?.Log(SOCKET_TXT, "disconnecting ...");
            if (websocket != null)
            {
                await websocket.Close();
                websocket = null;
            }

            if (callback != null)
            {
                callback.Invoke();
            }
        }

        public async void Disconnect()
        {
            await DisconnectAsync();
        }

        public async Task DisconnectAsync()
        {
            if (websocket != null)
            {
                await websocket.Close();
                websocket = null;
            }
        }

        public async void Cleanup()
        {
            await CleanupAsync();
        }


        public async Task CleanupAsync()
        {
            var activeChannels = channels.FindAll(channel => channel.IsNotClosedOrLeaving);
            activeChannels.ForEach(channel => channel.Leave());
            await DisconnectAsync();
            SocketReconnectTimer.Reset();
        }

        public async void Cleanup(Action callback)
        {
            await CleanupAsync(callback);
        }

        public async Task CleanupAsync(Action callback)
        {
            var activeChannels = channels.FindAll(channel => channel.IsNotClosedOrLeaving);
            activeChannels.ForEach(channel => channel.Leave());
            await DisconnectAsync();
            SocketReconnectTimer.Reset();
        }

        public async void Connect()
        {
            await ConnectAsync();
        }


        public async Task ConnectAsync()
        {
            if (websocket != null)
            {
                return;
            }
            Logger?.Log(SOCKET_TXT, "connecting to host :" + Endpoint, BuildParams());

            websocket = new UnityWebSocket(Endpoint + BuildParams());

            websocket.OnOpen += OnWebSocketOpen;
            websocket.OnClose += OnWebSocketClose;
            websocket.OnError += OnWebSocketError;
            websocket.OnMessage += OnWebSocketMessage;
            await websocket.Connect();
        }

        void InitHeartBeatTimer()
        {
            if (HeartBeatTimer == null)
            {
                HeartBeatTimer = new FixedCallbackTimer(SendHeartBuffer, HeartbeatInterval, true, false, "heart-beat-timer");
            }
            else
            {
                HeartBeatTimer.Reset();
            }
            HeartBeatTimer.ScheduleTimeout();
        }

        void OnWebSocketOpen()
        {
            Logger?.Log(SOCKET_TXT, "open");
            FlushSendBuffer();
            SocketReconnectTimer.Reset();
            InitHeartBeatTimer();
            OnOpen?.Invoke();
        }



        void OnWebSocketClose(WebSocketCloseCode closeCode)
        {
            Logger?.Log(SOCKET_TXT, "close ", " code => " + (int)closeCode);
            TriggerChannelError();
            if (HeartBeatTimer != null)
            {
                HeartBeatTimer.Reset();
            }

            if (closeCode == WebSocketCloseCode.Normal || closeCode == WebSocketCloseCode.ProtocolError)
            {
                SocketReconnectTimer.Reset();
            }
            else
            {
                SocketReconnectTimer.ScheduleTimeout();
            }
            OnClose?.Invoke((int)closeCode);
        }

        void OnWebSocketError(string msg)
        {
            Logger?.Log(SOCKET_TXT, "error", msg);
            TriggerChannelError();
            OnError?.Invoke(msg);
        }

        void OnWebSocketMessage(byte[] data)
        {
            var raw = Encoding.UTF8.GetString(data);
            Logger?.Log(SOCKET_TXT, "received message", raw);
            Response msg = new Response(raw);
            Logger?.Log(SOCKET_TXT, "decoded response", "channel = " + msg.Topic + ", event = " + msg.Event + ", ref = " + msg.RequestId);
            if (msg.RequestId != null && msg.RequestId == pendingHeartBeatRef)
            {
                Logger?.Log(SOCKET_TXT, "heartbeat reply", "reset pending heartbeat");
                pendingHeartBeatRef = null;
            }
            else
            {
                TriggerChannels(msg);
            }
        }

        void TriggerChannels(Response msg)
        {
            var memberChannels = channels.FindAll(ch => ch.IsMember(msg));
            memberChannels.ForEach((ch) =>
            {
                var isMember = ch.IsMember(msg);
                Logger?.Log(SOCKET_TXT, "is member?", "channel: " + ch.Id + (isMember ? " is true" : " is false"));
                if (isMember)
                {
                    Logger?.Log(SOCKET_TXT, "channel", "trigger event: " + msg.Event);
                    ch.Trigger(msg.Event, msg);
                }
            });
        }

        void TriggerChannelError()
        {
            channels.ForEach(
                (channel) => channel.Trigger(Channel.PHX_ERROR)
            );
        }

        public SocketState State
        {
            get
            {
                if (websocket == null)
                {
                    return SocketState.Closed;
                }
                else if (websocket.State == WebSocketState.Connecting)
                {
                    return SocketState.Connecting;
                }
                else if (websocket.State == WebSocketState.Open)
                {
                    return SocketState.Open;
                }
                else if (websocket.State == WebSocketState.Closing)
                {
                    return SocketState.Closing;
                }
                else
                {
                    return SocketState.Closed;
                }
            }
        }

        public bool IsConnected => State == SocketState.Open;

        public void Remove(Channel channel)
        {
            channels.RemoveAll(c => channel.Id == c.Id);
        }

        public Channel GetChannel(string topic, string options)
        {
            var channel = channels.Find(x => x.Topic == topic);
            if (channel == null)
            {
                channel = new Channel(topic, this, options);
                channels.Add(channel);
            }

            return channel;
        }

        public async void SendHeartBeatMsg()
        {
            var msg = "[null,\"" + pendingHeartBeatRef + "\",\"phoenix\",\"heartbeat\",{}]";
            if (IsConnected)
            {
                await websocket.SendText(msg);
            }
        }

        public void Push(string msg)
        {
            async void callback()
            {
                Logger?.Log(SOCKET_TXT, "push", msg);
                await websocket.SendText(msg);
            }
            if (IsConnected)
            {
                callback();
            }
            else
            {
                SendBuffer.Add(callback);
            }
        }

        public string MakeRef()
        {
            Ref++;
            return (Ref).ToString();
        }

        void SendHeartBuffer()
        {
            if (!IsConnected)
            {
                return;
            }
            if (pendingHeartBeatRef != null)
            {
                pendingHeartBeatRef = null;
                Logger?.Log(SOCKET_TXT, "heartbeat timeout. Attempting to re-establish connection");
                
                _ = websocket.Close();
                return;
            }
            pendingHeartBeatRef = MakeRef();
            SendHeartBeatMsg();
        }

        void FlushSendBuffer()
        {
            if (IsConnected && SendBuffer.Count > 0)
            {
                SendBuffer.ForEach(callback => callback());
                SendBuffer.Clear();
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

        //void Encode(Message msg, Action<string> callback)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    //sb.Append("[");

        //    if (msg.JoinRef == null)
        //    {
        //        sb.Append("[null,");
        //    }
        //    else
        //    {
        //        sb.Append("[\"").Append(msg.JoinRef).Append("\",");
        //    }

        //    if (msg.Ref == null)
        //    {
        //        sb.Append("null,\"");
        //    }
        //    else
        //    {
        //        sb.Append("\"").Append(msg.Ref).Append("\",\"");
        //    }

        //    sb.Append(msg.Topic).Append("\",\"").Append(msg.Event).Append("\",");

        //    if (msg.Payload == null)
        //    {
        //        sb.Append("{}]");
        //    }
        //    else
        //    {
        //        sb.Append(msg.Payload.ToString()).Append("]");
        //    }
        //    callback(sb.ToString());
        //}

        //Decode(message, msg =>
        //{
        //    if (msg.Ref != null && msg.Ref == pendingHeartBeatRef)
        //    {
        //        pendingHeartBeatRef = null;
        //    }
        //    Logger?.Log(SOCKET_TXT, "decoded", string.Format("channel = {0}, event = {1}, ref = {2}", msg.Topic, msg.Event,
        //                          msg.Ref ?? ""));
        //    var memberChannels =
        //            channels.FindAll(
        //                channel => channel.IsMember(msg)
        //            );

        //    memberChannels.ForEach(channel => channel.Trigger(msg.Event, msg));
        //});


        //public void Push(Message msg)
        //{
        //    void callback()
        //    {
        //        Encode(msg, async result =>
        //        {
        //            Logger?.Log(SOCKET_TXT, "push message", result);
        //            await websocket.SendText(result);
        //        });
        //    }

        //    if (IsConnected)
        //    {
        //        callback();
        //    }
        //    else
        //    {
        //        SendBuffer.Add(callback);
        //    }
        //}
    }
}