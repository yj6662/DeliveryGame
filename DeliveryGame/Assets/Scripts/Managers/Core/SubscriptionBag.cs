using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class SubscriptionBag : IDisposable
    {
        private List<Action> _unsubActions = new List<Action>(8);

        public void Add<T>(EventBus bus, Action<T> handler)
        {
            if (bus == null)
            {
                Debug.LogError("[SubscriptionBag] EventBus is null.");
                return;
            }

            if (handler == null)
            {
                Debug.LogError("[SubscriptionBag] Handler is null.");
                return;
            }

            bus.Subscribe(handler);
            _unsubActions.Add(() => bus.Unsubscribe(handler));
        }

        public void Clear()
        {
            for (int i = _unsubActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    Action unsub = _unsubActions[i];
                    if (unsub != null)
                    {
                        unsub();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            _unsubActions.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
