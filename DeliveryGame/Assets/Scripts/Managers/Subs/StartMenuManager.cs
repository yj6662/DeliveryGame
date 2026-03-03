using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    public sealed class StartMenuManager : SubManagerBase
    {
        private StartMenuRuntime _runtime;

        public override string Name => nameof(StartMenuManager);
        public override int InitOrder => 31;

        protected override void OnInitialize()
        {
            _runtime = new StartMenuRuntime(Services, Events);
            _runtime.Initialize();

            Subs.Add<SceneTransitionStarted>(Events, OnTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnTransitionCompleted);
            Subs.Add<MetaSaveSlotLoaded>(Events, OnMetaSaveSlotLoaded);
            Subs.Add<MetaSaveSlotSaved>(Events, OnMetaSaveSlotSaved);
            Subs.Add<MetaBalanceChanged>(Events, OnMetaDirty);
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

        private void OnMetaSaveSlotLoaded(MetaSaveSlotLoaded evt)
        {
            _runtime?.OnMetaDirty();
        }

        private void OnMetaSaveSlotSaved(MetaSaveSlotSaved evt)
        {
            _runtime?.OnMetaDirty();
        }

        private void OnMetaDirty(MetaBalanceChanged evt)
        {
            _runtime?.OnMetaDirty();
        }
    }
}
