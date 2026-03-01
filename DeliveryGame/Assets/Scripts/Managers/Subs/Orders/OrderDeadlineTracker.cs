using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderDeadlineTracker
    {
        internal int CopyTimerViewsNonAlloc(
            List<OrderRuntimeActiveOrder> activeOrders,
            ActiveOrderTimerView[] destination,
            double now)
        {
            if (destination == null || destination.Length <= 0 || activeOrders == null || activeOrders.Count <= 0)
            {
                return 0;
            }

            int count = activeOrders.Count < destination.Length ? activeOrders.Count : destination.Length;
            for (int i = 0; i < count; i++)
            {
                OrderRuntimeActiveOrder order = activeOrders[i];
                if (order == null)
                {
                    destination[i] = default;
                    continue;
                }

                float remaining = (float)Math.Max(0d, order.DeliveryDeadlineAt - now);
                destination[i] = new ActiveOrderTimerView
                {
                    OfferId = order.OfferId,
                    FoodName = order.FoodName,
                    RemainingSeconds = remaining,
                    LimitSeconds = Mathf.Max(1f, order.DeliveryLimitSeconds),
                    IsSeafood = order.FoodIsSeafood,
                    IsCarrying = order.Stage == OrderStage.Carrying
                };
            }

            return count;
        }

        internal void TickDeadlines(
            List<OrderRuntimeActiveOrder> activeOrders,
            double now,
            Action<int, OrderRuntimeActiveOrder> onTimedOut)
        {
            if (activeOrders == null || activeOrders.Count <= 0 || onTimedOut == null)
            {
                return;
            }

            for (int i = activeOrders.Count - 1; i >= 0; i--)
            {
                OrderRuntimeActiveOrder order = activeOrders[i];
                if (order == null)
                {
                    activeOrders.RemoveAt(i);
                    continue;
                }

                if (now < order.DeliveryDeadlineAt)
                {
                    continue;
                }

                onTimedOut(i, order);
            }
        }
    }
}
