using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    public sealed class TrafficNpcManager : SubManagerBase
    {
        private TrafficNpcRuntime _runtime;

        public override string Name => nameof(TrafficNpcManager);
        public override int InitOrder => 69;

        protected override void OnInitialize()
        {
            _runtime = new TrafficNpcRuntime(Services, Events);
            _runtime.Initialize();

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<TrafficSystemDefined>(Events, OnTrafficSystemDefined);
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

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            _runtime?.OnSceneTransitionStarted(evt);
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _runtime?.OnSceneTransitionCompleted(evt);
        }

        private void OnTrafficSystemDefined(TrafficSystemDefined evt)
        {
            _runtime?.OnTrafficSystemDefined(evt);
        }
    }
}
