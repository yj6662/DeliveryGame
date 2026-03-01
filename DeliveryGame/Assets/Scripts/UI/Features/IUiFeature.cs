using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;

namespace DeliveryRun.UI.Features
{
    internal interface IUiFeature
    {
        void Initialize(CoreContext ctx, UIManager ui);
        void Tick(float unscaledDeltaTime);
        void Shutdown();
    }
}
