using System.Collections.Generic;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderObjectivePublisher
    {
        internal struct ObjectiveOrderSnapshot
        {
            internal string OfferId;
            internal string PickupName;
            internal string DeliveryName;
            internal bool IsCarrying;
            internal OrderInteractPoint PickupInteract;
            internal OrderInteractPoint DeliveryInteract;
        }

        internal void Publish(EventBus events, List<ObjectiveOrderSnapshot> orders, bool hasPlayer, Vector3 playerPosition)
        {
            if (events == null)
            {
                return;
            }

            if (orders == null || orders.Count <= 0)
            {
                PublishObjectiveMarkerInactive(events);
                PublishObjectiveMarkersSnapshot(events, orders);
                return;
            }

            int primaryIndex = 0;
            for (int i = 0; i < orders.Count; i++)
            {
                if (!orders[i].IsCarrying)
                {
                    continue;
                }

                primaryIndex = i;
                break;
            }

            ObjectiveOrderSnapshot primary = orders[primaryIndex];
            OrderInteractPoint interactPoint = primary.IsCarrying ? primary.DeliveryInteract : primary.PickupInteract;
            string prefix = primary.IsCarrying ? "DELIVER TO: " : "GO PICKUP: ";
            string destination = primary.IsCarrying ? primary.DeliveryName : primary.PickupName;

            float distance = -1f;
            if (hasPlayer && interactPoint != null)
            {
                distance = Vector3.Distance(playerPosition, interactPoint.transform.position);
            }

            string text = prefix + destination;
            if (distance >= 0f)
            {
                text += " (" + Mathf.RoundToInt(distance) + "m)";
            }

            events.Publish(new OrderObjectiveUpdated
            {
                OfferId = primary.OfferId,
                Text = text,
                DistanceMeters = distance
            });

            PublishObjectiveMarker(events, primary.OfferId, interactPoint, primary.IsCarrying);
            PublishObjectiveMarkersSnapshot(events, orders);
        }

        private static void PublishObjectiveMarker(EventBus events, string offerId, OrderInteractPoint point, bool isCarrying)
        {
            if (point == null)
            {
                PublishObjectiveMarkerInactive(events);
                return;
            }

            events.Publish(new OrderObjectiveMarkerUpdated
            {
                OfferId = offerId,
                Active = true,
                PointType = isCarrying ? OrderPointType.Delivery : OrderPointType.Pickup,
                WorldPosition = point.transform.position
            });
        }

        private static void PublishObjectiveMarkerInactive(EventBus events)
        {
            events.Publish(new OrderObjectiveMarkerUpdated
            {
                OfferId = string.Empty,
                Active = false,
                PointType = OrderPointType.Pickup,
                WorldPosition = Vector3.zero
            });
        }

        private static void PublishObjectiveMarkersSnapshot(EventBus events, List<ObjectiveOrderSnapshot> orders)
        {
            OrderObjectiveMarkersUpdated markers = new OrderObjectiveMarkersUpdated
            {
                Count = 0,
                OfferId0 = string.Empty,
                OfferId1 = string.Empty,
                OfferId2 = string.Empty
            };

            if (orders == null || orders.Count <= 0)
            {
                events.Publish(markers);
                return;
            }

            int count = 0;
            for (int i = 0; i < orders.Count; i++)
            {
                if (count >= 3)
                {
                    break;
                }

                ObjectiveOrderSnapshot order = orders[i];
                OrderInteractPoint interactPoint = order.IsCarrying ? order.DeliveryInteract : order.PickupInteract;
                if (interactPoint == null)
                {
                    continue;
                }

                OrderPointType pointType = order.IsCarrying ? OrderPointType.Delivery : OrderPointType.Pickup;
                if (count == 0)
                {
                    markers.OfferId0 = order.OfferId;
                    markers.PointType0 = pointType;
                    markers.WorldPosition0 = interactPoint.transform.position;
                }
                else if (count == 1)
                {
                    markers.OfferId1 = order.OfferId;
                    markers.PointType1 = pointType;
                    markers.WorldPosition1 = interactPoint.transform.position;
                }
                else
                {
                    markers.OfferId2 = order.OfferId;
                    markers.PointType2 = pointType;
                    markers.WorldPosition2 = interactPoint.transform.position;
                }

                count++;
            }

            markers.Count = count;
            events.Publish(markers);
        }
    }
}
