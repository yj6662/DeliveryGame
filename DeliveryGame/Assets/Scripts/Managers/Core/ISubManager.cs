namespace DeliveryRun.Managers.Core
{
    public interface ISubManager
    {
        string Name { get; }
        int InitOrder { get; }
        void Initialize(CoreContext ctx);
        void Tick(float unscaledDeltaTime);
        void Shutdown();
    }
}
