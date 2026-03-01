using DeliveryRun.Managers.Core;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class SectorThemeManager : SubManagerBase
    {
        private SectorThemeRuntime _runtime;

        public override string Name => nameof(SectorThemeManager);
        public override int InitOrder => 47;

        protected override void OnInitialize()
        {
            _runtime = new SectorThemeRuntime(Services, Events);
            _runtime.Initialize();

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<SelectedRegionChanged>(Events, OnSelectedRegionChanged);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
        }

        protected override void OnShutdown()
        {
            _runtime?.Shutdown();
            _runtime = null;
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runtime?.OnRunStarted(evt);
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runtime?.OnRunEnded(evt);
        }

        private void OnSelectedRegionChanged(SelectedRegionChanged evt)
        {
            _runtime?.OnSelectedRegionChanged(evt);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            _runtime?.OnSceneTransitionStarted(evt);
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _runtime?.OnSceneTransitionCompleted(evt);
        }
    }
}
