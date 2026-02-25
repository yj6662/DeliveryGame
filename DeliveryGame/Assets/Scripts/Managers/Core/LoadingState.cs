namespace DeliveryRun.Managers.Core
{
    public sealed class LoadingState
    {
        public bool IsTransitioning;
        public string FromScene;
        public string ToScene;
        public string Phase;
        public float Progress01;

        public void Reset()
        {
            IsTransitioning = false;
            FromScene = null;
            ToScene = null;
            Phase = null;
            Progress01 = 0f;
        }
    }
}
