using System;
using UnityEngine;
using UnityToolbag;

namespace Indy.Phoenix
{
    public class UnityLogger : ILogger
    {

        public void Log(string kind, string msg, string data)
        {
            Debug.Log(kind + " - " + msg + " - " + data);
        }

        public void Log(string kind, string msg)
        {
            Debug.Log(kind + " - " + msg);
        }

        public void Log(string message)
        {
            Debug.Log(message);
        }
    }

    public class IsMainThreadLogger : ILogger
    {
        public void Log(string kind, string msg, string data = "")
        {
            if (Dispatcher._instance == null)
            {
                throw new Exception("To use IsMainThreadLogger, you need to add Dispatcher as component to a gameObject in the scene");
            }
            Log(kind + " - " + msg + " - " + data);
        }

        public void Log(string kind, string msg)
        {
            if (Dispatcher._instance == null)
            {
                throw new Exception("To use IsMainThreadLogger, you need to add Dispatcher as component to a gameObject in the scene");
            }
            Log(kind + " - " + msg);
        }

        public void Log(string msg)
        {
            if (Dispatcher._instance == null)
            {
                throw new Exception("To use IsMainThreadLogger, you need to add Dispatcher as component to a gameObject in the scene");
            }
            Debug.Log(" ===> is main thread - " + Dispatcher.IsMainThread + "\n ## " + msg);
        }
    }
}

