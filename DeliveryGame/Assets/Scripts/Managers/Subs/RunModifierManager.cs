using DeliveryRun.Managers.Core;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunModifierManager : SubManagerBase
    {
        private RunModifierRuntime _runtime;

        public override string Name => nameof(RunModifierManager);
        public override int InitOrder => 45;

        protected override void OnInitialize()
        {
            _runtime = new RunModifierRuntime(Services, Events);
            _runtime.Initialize();

            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunSessionEnded);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
        }

        protected override void OnShutdown()
        {
            _runtime?.Shutdown();
            _runtime = null;
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            _runtime?.OnRunSessionStarted();
        }

        private void OnRunSessionEnded(DomainRunSessionEnded evt)
        {
            _runtime?.OnRunSessionEnded();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            _runtime?.OnSceneTransitionStarted(evt);
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _runtime?.OnMusicChoiceSelected(evt);
        }
    }
}
