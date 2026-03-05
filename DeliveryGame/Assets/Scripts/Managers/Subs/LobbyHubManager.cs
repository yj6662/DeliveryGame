using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    public sealed class LobbyHubManager : SubManagerBase
    {
        private LobbyHubRuntime _runtime;

        public override string Name => nameof(LobbyHubManager);
        public override int InitOrder => 33;

        protected override void OnInitialize()
        {
            // Legacy runtime-generated Lobby UI is intentionally disabled.
            // LobbyScene now uses prefab/bootstrap driven UI.
            _runtime = null;
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _runtime?.Tick(unscaledDeltaTime);
        }

        protected override void OnShutdown()
        {
            _runtime?.Shutdown();
            _runtime = null;
        }

        private void OnTransitionStarted(SceneTransitionStarted evt)
        {
            _runtime?.OnTransitionStarted(evt);
        }

        private void OnTransitionCompleted(SceneTransitionCompleted evt)
        {
            _runtime?.OnTransitionCompleted(evt);
        }

        private void OnMetaBalanceChanged(MetaBalanceChanged evt)
        {
            _runtime?.OnMetaDirty();
        }

        private void OnPermanentUpgradeChanged(PermanentUpgradeChanged evt)
        {
            _runtime?.OnMetaDirty();
        }

        private void OnSelectedRegionChanged(SelectedRegionChanged evt)
        {
            _runtime?.OnMetaDirty();
        }

        private void OnRegionUnlockStatusChanged(RegionUnlockStatusChanged evt)
        {
            _runtime?.OnMetaDirty();
        }

        private void OnRegionUnlocked(RegionUnlocked evt)
        {
            _runtime?.OnMetaDirty();
        }

        private void OnRegionUnlockFailed(RegionUnlockFailed evt)
        {
            _runtime?.OnMetaDirty();
        }
    }
}
