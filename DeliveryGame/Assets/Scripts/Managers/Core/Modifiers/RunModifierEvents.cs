namespace DeliveryRun.Managers.Core
{
    public struct PlayerMoveSpeedMultiplierChanged
    {
        public float Multiplier;
        public string SourceId;
    }

    public struct RunModifiersCleared
    {
    }

    public struct RunModifiersChanged
    {
        public string SourceId;
    }
}
