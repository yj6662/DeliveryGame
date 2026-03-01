using DeliveryRun.Managers.Core;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class MusicChoiceManager : SubManagerBase
    {
        private MusicChoiceRuntime _runtime;

        public override string Name => nameof(MusicChoiceManager);
        public override int InitOrder => 37;

        public bool IsRunActive => _runtime != null && _runtime.IsRunActive;
        public bool HasPendingChoice => _runtime != null && _runtime.HasPendingChoice;
        public int PendingChoiceIndex => _runtime != null ? _runtime.PendingChoiceIndex : -1;
        public float PendingChoiceRemainingSeconds => _runtime != null ? _runtime.PendingChoiceRemainingSeconds : 0f;

        protected override void OnInitialize()
        {
            _runtime = new MusicChoiceRuntime(Services, Events);
            _runtime.Initialize();

            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunSessionEnded);
            Subs.Add<MusicDraftGenerated>(Events, OnMusicDraftGenerated);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
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

        public void ApplyChoice(string trackId)
        {
            _runtime?.ApplyChoice(trackId);
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            _runtime?.OnRunSessionStarted();
        }

        private void OnRunSessionEnded(DomainRunSessionEnded evt)
        {
            _runtime?.OnRunSessionEnded();
        }

        private void OnMusicDraftGenerated(MusicDraftGenerated evt)
        {
            _runtime?.OnMusicDraftGenerated(evt);
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _runtime?.OnMusicChoiceSelected(evt);
        }
    }
}
