using DeliveryRun.Delivery.Orders;

namespace DeliveryRun.Managers.Subs
{
    internal enum OrderStage
    {
        AwaitPickup = 0,
        Carrying = 1
    }

    internal sealed class OrderRuntimeActiveOrder
    {
        internal int OrderId;
        internal string OfferId;
        internal string PickupName;
        internal string DeliveryName;
        internal int BaseReward;
        internal OrderStage Stage;
        internal double AcceptedAt;
        internal double PickedUpAt;
        internal OrderInteractPoint PickupInteract;
        internal OrderInteractPoint DeliveryInteract;
        internal OrderBuildingAnchor RestaurantAnchor;
        internal OrderBuildingAnchor DestinationAnchor;
        internal string FoodId;
        internal string FoodName;
        internal float FoodTempDecayMultiplier;
        internal float FoodSpillGainMultiplier;
        internal float DeliveryLimitSeconds;
        internal double DeliveryDeadlineAt;
        internal bool FoodIsSeafood;
        internal string RegionId;
    }

    internal struct OrderPendingOffer
    {
        internal bool Active;
        internal int OrderId;
        internal string OfferId;
        internal string PickupName;
        internal string DeliveryName;
        internal int Reward;
        internal double EndTime;
        internal OrderBuildingAnchor RestaurantAnchor;
        internal OrderBuildingAnchor DestinationAnchor;
        internal string FoodId;
        internal string FoodName;
        internal float FoodTempDecayMultiplier;
        internal float FoodSpillGainMultiplier;
        internal float DeliveryLimitSeconds;
        internal bool FoodIsSeafood;
        internal string RegionId;
    }

    public struct ActiveOrderTimerView
    {
        public string OfferId;
        public string FoodName;
        public float RemainingSeconds;
        public float LimitSeconds;
        public bool IsSeafood;
        public bool IsCarrying;
    }
}
