namespace DeliveryRun.Managers.Core
{
    public struct RatingChanged
    {
        public float Value;
        public float Delta;
    }

    public struct RatingDepleted
    {
    }

    public struct EconomyChanged
    {
        public int SessionCoins;
        public int TotalCoins;
        public int Delta;
        public bool IsRunActive;
    }
}
