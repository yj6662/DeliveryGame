using System.Collections.Generic;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderInteractPointController
    {
        private const float InteractRadius = 4f;
        private const float SpawnRetryInterval = 0.35f;

        private readonly OrderInteractPointFactory _factory;
        private readonly OrderRoadSideLocator _roadSideLocator;

        private float _retryAccum;

        public OrderInteractPointController(OrderInteractPointFactory factory, OrderRoadSideLocator roadSideLocator)
        {
            _factory = factory;
            _roadSideLocator = roadSideLocator;
        }

        public float RetryAccum
        {
            get => _retryAccum;
            set => _retryAccum = value;
        }

        public void EnsureInteractPoints(
            List<OrderRuntimeActiveOrder> activeOrders,
            float unscaledDeltaTime,
            RoadQueryManager roadQuery,
            ServiceRegistry services,
            EventBus events)
        {
            if (activeOrders.Count <= 0)
            {
                return;
            }

            _retryAccum += unscaledDeltaTime;
            if (_retryAccum < SpawnRetryInterval)
            {
                return;
            }

            _retryAccum = 0f;
            for (int i = 0; i < activeOrders.Count; i++)
            {
                OrderRuntimeActiveOrder order = activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                if (order.Stage == OrderStage.AwaitPickup)
                {
                    if (order.PickupInteract == null)
                    {
                        SpawnPickup(order, roadQuery, services, events);
                    }

                    continue;
                }

                if (order.DeliveryInteract == null)
                {
                    SpawnDelivery(order, roadQuery, services, events);
                }
            }
        }

        public bool TryHandleInput(
            List<OrderRuntimeActiveOrder> activeOrders,
            MotorbikeController player)
        {
            if (activeOrders.Count <= 0)
            {
                return false;
            }

            OrderInteractPoint candidate = SelectBestCandidate(activeOrders, player);
            if (candidate == null)
            {
                return false;
            }

            if (!RuntimeInput.ConsumeInteractPressedThisFrame())
            {
                return false;
            }

            candidate.TryRequestInteract();
            return true;
        }

        public bool SpawnPickup(
            OrderRuntimeActiveOrder order,
            RoadQueryManager roadQuery,
            ServiceRegistry services,
            EventBus events)
        {
            if (order == null)
            {
                return false;
            }

            DespawnPickup(order);
            if (roadQuery == null)
            {
                services.TryGet(out roadQuery);
            }

            Vector3 spawnPosition;
            if (!_roadSideLocator.TryResolve(order.RestaurantAnchor, roadQuery, out spawnPosition))
            {
                return false;
            }

            order.PickupInteract = _factory.Create(
                "OrderInteractPoint_Pickup_" + order.OrderId,
                order.OrderId,
                OrderPointType.Pickup,
                "P" + order.OrderId,
                order.PickupName,
                spawnPosition,
                InteractRadius,
                point => { if (point != null) events.Publish(new OrderInteractRequested(point.OrderId, point.PointType)); });
            return order.PickupInteract != null;
        }

        public bool SpawnDelivery(
            OrderRuntimeActiveOrder order,
            RoadQueryManager roadQuery,
            ServiceRegistry services,
            EventBus events)
        {
            if (order == null)
            {
                return false;
            }

            DespawnDelivery(order);
            if (roadQuery == null)
            {
                services.TryGet(out roadQuery);
            }

            Vector3 spawnPosition;
            if (!_roadSideLocator.TryResolve(order.DestinationAnchor, roadQuery, out spawnPosition))
            {
                return false;
            }

            order.DeliveryInteract = _factory.Create(
                "OrderInteractPoint_Delivery_" + order.OrderId,
                order.OrderId,
                OrderPointType.Delivery,
                "D" + order.OrderId,
                order.DeliveryName,
                spawnPosition,
                InteractRadius,
                point => { if (point != null) events.Publish(new OrderInteractRequested(point.OrderId, point.PointType)); });
            return order.DeliveryInteract != null;
        }

        public void DespawnPickup(OrderRuntimeActiveOrder order)
        {
            if (order == null || order.PickupInteract == null)
            {
                return;
            }

            GameObject target = order.PickupInteract.gameObject;
            order.PickupInteract = null;
            if (target != null)
            {
                Object.Destroy(target);
            }
        }

        public void DespawnDelivery(OrderRuntimeActiveOrder order)
        {
            if (order == null || order.DeliveryInteract == null)
            {
                return;
            }

            GameObject target = order.DeliveryInteract.gameObject;
            order.DeliveryInteract = null;
            if (target != null)
            {
                Object.Destroy(target);
            }
        }

        public void DespawnAll(List<OrderRuntimeActiveOrder> activeOrders)
        {
            for (int i = 0; i < activeOrders.Count; i++)
            {
                OrderRuntimeActiveOrder order = activeOrders[i];
                DespawnPickup(order);
                DespawnDelivery(order);
            }
        }

        private OrderInteractPoint SelectBestCandidate(
            List<OrderRuntimeActiveOrder> activeOrders,
            MotorbikeController player)
        {
            OrderInteractPoint bestPoint = null;
            bool bestIsDelivery = false;
            float bestSqrDistance = float.MaxValue;
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            bool hasPlayer = player != null;

            for (int i = 0; i < activeOrders.Count; i++)
            {
                OrderRuntimeActiveOrder order = activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                bool isDelivery = order.Stage == OrderStage.Carrying;
                OrderInteractPoint point = isDelivery ? order.DeliveryInteract : order.PickupInteract;
                if (point == null || !point.IsArmed || !point.IsPlayerInRange)
                {
                    continue;
                }

                float sqrDistance = 0f;
                if (hasPlayer)
                {
                    sqrDistance = (point.transform.position - playerPos).sqrMagnitude;
                }

                if (bestPoint == null)
                {
                    bestPoint = point;
                    bestIsDelivery = isDelivery;
                    bestSqrDistance = sqrDistance;
                    continue;
                }

                if (isDelivery && !bestIsDelivery)
                {
                    bestPoint = point;
                    bestIsDelivery = true;
                    bestSqrDistance = sqrDistance;
                    continue;
                }

                if (isDelivery == bestIsDelivery && sqrDistance < bestSqrDistance)
                {
                    bestPoint = point;
                    bestSqrDistance = sqrDistance;
                }
            }

            return bestPoint;
        }

    }
}
