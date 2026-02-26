namespace DeliveryRun.Managers.Core
{
    public struct FoodStateTicked
    {
        public float Temperature01;
        public float Spill01;
        public float Quality01;
    }

    public struct FoodQualityComputed
    {
        public string OfferId;
        public float Temperature01;
        public float Spill01;
        public float Quality01;
        public float RewardMultiplier;
        public float RatingDelta;
    }
}
