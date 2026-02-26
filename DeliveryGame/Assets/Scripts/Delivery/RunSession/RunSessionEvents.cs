namespace DeliveryRun.Delivery.RunSession
{
    public struct RunSessionStarted
    {
        public float DurationSeconds;
    }

    public struct RunSessionStateChanged
    {
        public RunSessionState From;
        public RunSessionState To;
    }

    public struct RunTimerTicked
    {
        public float ElapsedSeconds;
        public float RemainingSeconds;
    }

    public struct RunChoicePointReached
    {
        public int Index;
        public float AtElapsedSeconds;
    }

    public struct RunLastOrderStarted
    {
        public float AtElapsedSeconds;
    }

    public struct RunSessionEnded
    {
        public RunEndReason Reason;
        public float ElapsedSeconds;
    }
}
