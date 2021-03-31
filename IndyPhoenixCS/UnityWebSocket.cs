﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NativeWebSocket;


namespace Indy.Phoenix
{
    public class UnityWebSocket : WebSocket
    {
        /// <summary>
        ///     Is the connection currently open
        /// </summary>
        public bool IsOpen;

        /// <summary>
        ///     Flag to keep processing function alive
        /// </summary>
        /// <remarks>Set to true via <see cref="_OnOpen" />, false via <see cref="_OnClose" /></remarks>
        protected bool ProcessingMessageQueue;

        public UnityWebSocket(string url) : base(url)
        {
            Initialize();
        }

        public UnityWebSocket(string url, Dictionary<string, string> headers) : base(url, headers)
        {
            Initialize();
        }

        private void Initialize()
        {
            OnOpen += _OnOpen;
            OnClose += _OnClose;
        }


#if UNITY_WEBGL && !UNITY_EDITOR
#else
        /// <summary>
        ///     A while loop that runs as long as the connection is open, triggering <see cref="WebSocket.DispatchMessageQueue" />
        /// </summary>
        public async void ProcessMessageQueue()
        {
            ProcessingMessageQueue = true;
            while (ProcessingMessageQueue)
            {
                DispatchMessageQueue();

                // probably should be waiting until a new frame started or so
                await Task.Delay(TimeSpan.FromSeconds(1.0f / 120.0f)); //TODO: Some magic numbers here
            }
        }
#endif

        /// <summary>
        ///     Functionality to run when connection is opened
        /// </summary>
        /// <remarks>Kick starts the <see cref="ProcessMessageQueue" /> while loop</remarks>
        protected void _OnOpen()
        {
            IsOpen = true;

#if UNITY_WEBGL && !UNITY_EDITOR
#else
            ProcessMessageQueue();
#endif
        }

        /// <summary>
        ///     Functionality to run when a connection closes
        /// </summary>
        /// <remarks>
        ///     Sets the <see cref="ProcessingMessageQueue" /> flag to false, stopping the
        ///     <see cref="ProcessingMessageQueue" /> while loop
        /// </remarks>
        /// <param name="code">The cause of the socket closure</param>
        protected void _OnClose(WebSocketCloseCode code)
        {
            ProcessingMessageQueue = false;
            IsOpen = false;
        }
    }
}
