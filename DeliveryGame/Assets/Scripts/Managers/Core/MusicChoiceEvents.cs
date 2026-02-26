namespace DeliveryRun.Managers.Core
{
    public struct MusicChoiceSelected
    {
        public int ChoiceIndex;
        public int OptionIndex;
        public string TrackId;
        public string GenreId;
    }

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
