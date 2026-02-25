using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class GameClock
    {
        private struct TimerEntry
        {
            public int Handle;
            public double DueTime;
            public Action Callback;
        }

        private readonly List<TimerEntry> _timers = new List<TimerEntry>(32);
        private int _nextHandle = 1;

        public double Now { get; private set; }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime < 0f)
            {
                unscaledDeltaTime = 0f;
            }

            Now += unscaledDeltaTime;

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                TimerEntry entry = _timers[i];
                if (entry.DueTime > Now)
                {
                    continue;
                }

                _timers.RemoveAt(i);

                Action callback = entry.Callback;
                if (callback == null)
                {
                    continue;
                }

                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public int Schedule(float delay, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (delay < 0f)
            {
                delay = 0f;
            }

            int handle = _nextHandle++;
            if (_nextHandle <= 0)
            {
                _nextHandle = 1;
            }

            TimerEntry entry = new TimerEntry
            {
                Handle = handle,
                DueTime = Now + delay,
                Callback = callback
            };

            _timers.Add(entry);
            return handle;
        }

        public bool Cancel(int handle)
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                if (_timers[i].Handle != handle)
                {
                    continue;
                }

                _timers.RemoveAt(i);
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _timers.Clear();
        }
    }
}
