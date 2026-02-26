namespace DeliveryRun.Managers.Core
{
    public readonly struct RunModifier
    {
        public readonly string SourceId;
        public readonly RunStatId Stat;
        public readonly ModifierMode Mode;
        public readonly float Value;

        public RunModifier(string sourceId, RunStatId stat, ModifierMode mode, float value)
        {
            SourceId = sourceId;
            Stat = stat;
            Mode = mode;
            Value = value;
        }
    }
}
