using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>(64);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int RecentEventCapacity = 32;
        private readonly string[] _recentEventTypes = new string[RecentEventCapacity];
        private int _recentWriteIndex;
        private int _recentCount;
#endif

        public void Subscribe<T>(Action<T> handler)
        {
            Subscribe(handler, false);
        }

        public void Subscribe<T>(Action<T> handler, bool preventDuplicate)
        {
            if (handler == null)
            {
                Debug.LogError("[EventBus] Cannot subscribe with a null handler.");
                return;
            }

            Type eventType = typeof(T);
            Delegate existing;
            if (_handlers.TryGetValue(eventType, out existing))
            {
                if (preventDuplicate && IsAlreadySubscribed(existing, handler))
                {
                    return;
                }

                _handlers[eventType] = Delegate.Combine(existing, handler);
                return;
            }

            _handlers.Add(eventType, handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            Type eventType = typeof(T);
            Delegate existing;
            if (!_handlers.TryGetValue(eventType, out existing))
            {
                return;
            }

            Delegate updated = Delegate.Remove(existing, handler);
            if (updated == null)
            {
                _handlers.Remove(eventType);
                return;
            }

            _handlers[eventType] = updated;
        }

        public void Publish<T>(T evt)
        {
            Type eventType = typeof(T);
            RecordRecentEvent(eventType);

            Delegate callback;
            if (!_handlers.TryGetValue(eventType, out callback))
            {
                return;
            }

            Action<T> action = callback as Action<T>;
            if (action == null)
            {
                Debug.LogError("[EventBus] Handler type mismatch: " + eventType.FullName);
                return;
            }

            action(evt);
        }

        public void ClearAll()
        {
            _handlers.Clear();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Array.Clear(_recentEventTypes, 0, _recentEventTypes.Length);
            _recentWriteIndex = 0;
            _recentCount = 0;
#endif
        }

        public int CopyRecentEventsNonAlloc(string[] destination)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (destination == null || destination.Length == 0)
            {
                return 0;
            }

            int count = _recentCount < destination.Length ? _recentCount : destination.Length;
            for (int i = 0; i < count; i++)
            {
                int index = _recentWriteIndex - 1 - i;
                if (index < 0)
                {
                    index += RecentEventCapacity;
                }

                destination[i] = _recentEventTypes[index];
            }

            return count;
#else
            return 0;
#endif
        }

        private void RecordRecentEvent(Type eventType)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (eventType == null)
            {
                return;
            }

            _recentEventTypes[_recentWriteIndex] = eventType.Name;
            _recentWriteIndex++;
            if (_recentWriteIndex >= RecentEventCapacity)
            {
                _recentWriteIndex = 0;
            }

            if (_recentCount < RecentEventCapacity)
            {
                _recentCount++;
            }
#endif
        }

        private static bool IsAlreadySubscribed<T>(Delegate existing, Action<T> handler)
        {
            Delegate[] invocationList = existing.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                if (invocationList[i] == (Delegate)handler)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
