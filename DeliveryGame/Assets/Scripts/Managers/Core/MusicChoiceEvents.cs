namespace DeliveryRun.Managers.Core
{
    public struct MusicChoiceRequested
    {
        public int RunSequence;
        public int ChoiceIndex;
        public float ElapsedSeconds;
        public float DecisionTimeLimitSeconds;
    }

    public struct MusicChoiceAutoResolved
    {
        public int RunSequence;
        public int ChoiceIndex;
        public string ModifierId;
    }

    public struct MusicChoiceApplied
    {
        public int RunSequence;
        public int ChoiceIndex;
        public string ModifierId;
        public bool AutoSelected;
    }
}
