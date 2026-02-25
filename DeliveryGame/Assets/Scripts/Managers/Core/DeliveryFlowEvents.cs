namespace DeliveryRun.Managers.Core
{
    public enum DeliveryFailReason
    {
        None = 0,
        Timeout = 1,
        ManualAbort = 2
    }

    public struct DeliveryRunStarted
    {
        public int RunSequence;
    }

    public struct DeliveryRunEnded
    {
        public int RunSequence;
    }

    public struct DeliveryOrderSpawned
    {
        public int OrderSequence;
        public float TimeLimitSeconds;
    }

    public struct DeliveryOrderCompleted
    {
        public int OrderSequence;
        public int RewardCoins;
    }

    public struct DeliveryOrderFailed
    {
        public int OrderSequence;
        public DeliveryFailReason Reason;
    }
}
