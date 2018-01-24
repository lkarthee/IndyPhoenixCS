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
