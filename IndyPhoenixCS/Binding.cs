using System;
using Newtonsoft.Json.Linq;

namespace Indy.Phoenix
{
    public class Binding
    {
        public enum ParamType
        {
            NoParams,
            JObject,
            Message,
        }

        public int Ref { get; private set; }
        public string Name { get; private set; }

        Action Callback;
        Action<JObject> CallbackJObj;
        Action<Message> CallbackMsg;

        public ParamType Params { get; }

        public Binding(string name, Action<JObject> callback)
        {
            Name = name;
            CallbackJObj = callback;
            Params = ParamType.JObject;
        }

        public Binding(string name, int reff, Action<JObject> callback): this(name, callback)
        {
            Ref = reff;
        }

        public Binding(string name, Action<Message> callback)
        {
            Name = name;
            CallbackMsg = callback;
            Params = ParamType.Message;
        }

        public Binding(string name, int reff, Action<Message> callback): this(name, callback)
        {
            Ref = reff;
        }

        public Binding(string name, Action callback)
        {
            Name = name;
            Callback = callback;
            Params = ParamType.NoParams;
        }

        public Binding(string name, int reff, Action callback) : this(name, callback)
        {
            Ref = reff;
        }

        public void Invoke()
        {
            Callback();
        }

        public void Invoke(Object obj)
        {           
            if (Params == ParamType.NoParams)
            {
                Callback();
            } else if (Params == ParamType.JObject)
            {
                CallbackJObj(obj == null ? null : (JObject)obj);
            } else if (Params == ParamType.Message)
            {
                CallbackMsg(obj == null ? null : (Message)obj);
            }
        }

        public void InvokeJObj(JObject obj)
        {
                CallbackJObj(obj);
        }

        public void InvokeMsg(Message obj)
        {
                CallbackMsg(obj);
        }
    }
}
