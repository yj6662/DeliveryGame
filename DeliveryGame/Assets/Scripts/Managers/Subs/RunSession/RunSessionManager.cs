using DeliveryRun.Delivery.RunSession;
using DeliveryRun.Managers.Core;
using DomainRunSessionState = DeliveryRun.Delivery.RunSession.RunSessionState;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunSessionManager : SubManagerBase
    {
        private RunSessionRuntime _runtime;

        public override string Name => nameof(RunSessionManager);
        public override int InitOrder => 40;

        public DomainRunSessionState CurrentState => _runtime != null ? _runtime.CurrentState : DomainRunSessionState.Ready;
        public float RemainingSeconds => _runtime != null ? _runtime.RemainingSeconds : 0f;
        public bool HasActiveRun => _runtime != null && _runtime.HasActiveRun;
        public bool IsActive => HasActiveRun;
        public float ElapsedSeconds => _runtime != null ? _runtime.ElapsedSeconds : 0f;
        public float RunDurationSeconds => _runtime != null ? _runtime.RunDurationSeconds : 0f;
        public bool IsLastOrderPhase => _runtime != null && _runtime.IsLastOrderPhase;

        protected override void OnInitialize()
        {
            _runtime = new RunSessionRuntime(Services, Events);
            _runtime.Initialize();

            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<RatingZeroReached>(Events, OnRatingZeroReached);
            Subs.Add<RatingDepleted>(Events, OnRatingDepleted);
            Subs.Add<FuelDepleted>(Events, OnFuelDepleted);
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

        public void ForceEndRun()
        {
            _runtime?.ForceEndRun();
        }

        public void DebugAddElapsed(float seconds)
        {
            _runtime?.DebugAddElapsed(seconds);
        }

        public void DebugEnterChoicePause()
        {
            _runtime?.DebugEnterChoicePause();
        }

        public void DebugResumeFromChoice()
        {
            _runtime?.DebugResumeFromChoice();
        }

        public void EnterPauseForChoice()
        {
            _runtime?.EnterPauseForChoice();
        }

        public void ResumeFromChoice()
        {
            _runtime?.ResumeFromChoice();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _runtime?.OnSceneTransitionCompleted(evt);
        }

        private void OnRatingZeroReached(RatingZeroReached evt)
        {
            _runtime?.OnRatingZeroReached(evt);
        }

        private void OnRatingDepleted(RatingDepleted evt)
        {
            _runtime?.OnRatingDepleted(evt);
        }

        private void OnFuelDepleted(FuelDepleted evt)
        {
            _runtime?.OnFuelDepleted(evt);
        }
    }
}
