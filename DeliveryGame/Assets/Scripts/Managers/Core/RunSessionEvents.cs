namespace DeliveryRun.Managers.Core
{
    public enum RunEndReason
    {
        None = 0,
        TimeUp = 1,
        RatingDepleted = 2,
        SceneLeft = 3,
        FuelDepleted = 4
    }

    public struct RunSessionStarted
    {
        public int RunSequence;
        public float DurationSeconds;
    }

    public struct RunSessionTick
    {
        public int RunSequence;
        public float ElapsedSeconds;
        public float RemainingSeconds;
        public bool IsLastOrderPhase;
    }

    public struct RunSessionLastOrderStarted
    {
        public int RunSequence;
        public float ElapsedSeconds;
    }

    public struct RunSessionEnded
    {
        public int RunSequence;
        public RunEndReason Reason;
    }
}
