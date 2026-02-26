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
        public string FoodId;
        public string FoodName;
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
        public string FoodId;
        public string FoodName;
        public float TemperatureDecayMultiplier;
        public float SpillGainMultiplier;
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
        public string OfferId;
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

    public struct OrderObjectiveMarkersUpdated
    {
        public int Count;

        public string OfferId0;
        public OrderPointType PointType0;
        public Vector3 WorldPosition0;

        public string OfferId1;
        public OrderPointType PointType1;
        public Vector3 WorldPosition1;

        public string OfferId2;
        public OrderPointType PointType2;
        public Vector3 WorldPosition2;
    }
}
