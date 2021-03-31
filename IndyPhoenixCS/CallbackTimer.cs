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
using System.Timers;

namespace Indy.Phoenix
{
    public abstract class CallbackTimer
    {
        protected Timer timer;
        public int Tries { get; protected set; }
        public string Name { get; protected set; }
        protected Action Callback;
        public bool Recurring;
        public bool useMainThread;

        protected void Init(Action callback, bool autoReset, bool mainThread, string name = null)
        {
            useMainThread = mainThread;
            Callback = callback;
            Tries = 0;
            Recurring = autoReset;
            timer = new Timer { AutoReset = false };
            timer.Elapsed += OnTimerElapsed;
            Name = name;
        }

        private async void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            //Socket.Log("OnTimerElapsed - " + Name + " => tries = " + Tries);
            if (!Recurring)
            {
                Tries++;
            }
            if (useMainThread)
            {
                await new WaitForUpdate();
            }
            Callback();
        }

        public void Reset()
        {
            Tries = 0;
            timer.AutoReset = false;
            timer.Stop();
        }

        protected void ScheduleTimeout(TimeSpan timeSpan)
        {
            timer.Stop();
            timer.AutoReset = Recurring;
            timer.Interval = timeSpan.TotalMilliseconds;
            timer.Start();
        }

        public abstract void ScheduleTimeout();
    }

    public class FixedCallbackTimer : CallbackTimer
    {
        public readonly TimeSpan TimeSpan;

        public FixedCallbackTimer(Action callback, TimeSpan timespan, string name = null)
        {
            Init(callback, false, true, name);
            TimeSpan = timespan;
        }

        public FixedCallbackTimer(Action callback, TimeSpan timespan, bool recurring, string name = null)
        {
            Init(callback, recurring, true, name);
            TimeSpan = timespan;
        }

        public FixedCallbackTimer(Action callback, TimeSpan timespan, bool recurring, bool mainThread, string name = null)
        {
            Init(callback, recurring, mainThread, name);
            TimeSpan = timespan;
        }

        public override void ScheduleTimeout()
        {
            ScheduleTimeout(TimeSpan);
        }
    }

    public class FuncCallbackTimer : CallbackTimer
    {
        readonly Func<int, TimeSpan> timerCalcFunc;

        public FuncCallbackTimer(Action callback, Func<int, TimeSpan> func, bool recurring, string name = null)
        {
            Init(callback, recurring, true, name);
            timerCalcFunc = func;
        }

        public FuncCallbackTimer(Action callback, Func<int, TimeSpan> func, bool recurring, bool mainThread, string name = null)
        {
            Init(callback, recurring, mainThread, name);
            timerCalcFunc = func;
        }

        public FuncCallbackTimer(Action callback, Func<int, TimeSpan> func, string name = null)
        {
            Init(callback, false, true, name);
            timerCalcFunc = func;
        }

        public override void ScheduleTimeout()
        {
            var ts = timerCalcFunc(Tries);
            ScheduleTimeout(ts);
        }
    }
}
