using System;
using System.Timers;

namespace Indy.Phoenix
{
    public class CallbackTimer
    {
        public enum Mode
        {
            Func,
            Time
        }

        int tries;
        Timer timer;
        Action callback;
        readonly Func<int, TimeSpan> timerCalcFunc;
        readonly TimeSpan timespan;
        Mode mode;

        void Init(Action callbackF) {
            tries = 0;
            timer = new Timer();
            this.callback = callbackF;
            timer.Elapsed += (sender, e) => { tries++; callback(); };
        }

        public CallbackTimer(Action callback, Func<int, TimeSpan> timerCalcFunc)
        {
            Init(callback);
            this.timerCalcFunc = timerCalcFunc;
            this.mode = Mode.Func;
        }

        public CallbackTimer(Action callback, TimeSpan timespan)
        {
            Init(callback);
            this.timespan = timespan;
            this.mode = Mode.Time;
        }

        public void Reset()
        {
            tries = 0;
            timer.Stop();
        }

        public void ScheduleTimeout()
        {
            timer.Stop();

            if (this.mode == Mode.Func)
            {
                timer.Interval = timerCalcFunc(tries).TotalMilliseconds;
            } else {
                timer.Interval = timespan.TotalMilliseconds;
            }

            timer.Start();
        }
    }
}
