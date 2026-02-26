namespace DeliveryRun.Managers.Core
{
    public struct RatingChanged
    {
        public float Rating;
        public float Delta;
        public string Reason;

        // Legacy compatibility for existing listeners.
        public float Value
        {
            get { return Rating; }
            set { Rating = value; }
        }
    }

    public struct RatingZeroReached
    {
        public float Rating;
    }

    // Legacy compatibility event for existing listeners.
    public struct RatingDepleted
    {
    }
}
