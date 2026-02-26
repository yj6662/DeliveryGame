using DeliveryRun.Delivery.Orders;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public struct OfferSpawned
    {
        public string OfferId;
        public float TtlSeconds;
        public string PickupName;
        public string DeliveryName;
        public int Reward;
    }

    public struct OfferTicked
    {
        public string OfferId;
        public float RemainingSeconds;
    }

    public struct OfferExpired
    {
        public string OfferId;
    }

    public struct OfferAccepted
    {
        public string OfferId;
    }

    public struct OrderPickupReached
    {
        public string OfferId;
    }

    public struct OrderDeliveryReached
    {
        public string OfferId;
        public int Reward;
    }

    public struct OrderCompleted
    {
        public string OfferId;
        public int Reward;
    }

    public struct AcceptOfferRequested
    {
        public string OfferId;
    }

    public readonly struct OrderInteractRequested
    {
        public readonly int OrderId;
        public readonly OrderPointType PointType;

        public OrderInteractRequested(int orderId, OrderPointType pointType)
        {
            OrderId = orderId;
            PointType = pointType;
        }
    }

    public struct OrderObjectiveUpdated
    {
        public string Text;
        public float DistanceMeters;
    }

    public struct OrderObjectiveMarkerUpdated
    {
        public string OfferId;
        public bool Active;
        public OrderPointType PointType;
        public Vector3 WorldPosition;
    }
}
