using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RatingManager : SubManagerBase
    {
        private const float MaxRating = 5.0f;
        private const float MinRating = 0.0f;
        private const float SuccessDelta = 0.15f;
        private const float FailTimeoutDelta = -0.55f;
        private const float FailDefaultDelta = -0.35f;

        private bool _runActive;
        private bool _depletedPublished;
        private float _currentRating;

        public override string Name => nameof(RatingManager);
        public override int InitOrder => 45;

        public float CurrentRating => _currentRating;
        public bool IsDepleted => _currentRating <= MinRating;

        protected override void OnInitialize()
        {
            _runActive = false;
            _depletedPublished = false;
            _currentRating = MaxRating;

            Subs.Add<RunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<RunSessionEnded>(Events, OnRunSessionEnded);
            Subs.Add<DeliveryOrderCompleted>(Events, OnDeliveryOrderCompleted);
            Subs.Add<DeliveryOrderFailed>(Events, OnDeliveryOrderFailed);
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            _depletedPublished = false;
            _currentRating = MaxRating;
        }

        private void OnRunSessionStarted(RunSessionStarted evt)
        {
            _runActive = true;
            _depletedPublished = false;
            SetRating(MaxRating, 0f);
        }

        private void OnRunSessionEnded(RunSessionEnded evt)
        {
            _runActive = false;
        }

        private void OnDeliveryOrderCompleted(DeliveryOrderCompleted evt)
        {
            if (!_runActive)
            {
                return;
            }

            ApplyDelta(SuccessDelta);
        }

        private void OnDeliveryOrderFailed(DeliveryOrderFailed evt)
        {
            if (!_runActive)
            {
                return;
            }

            float delta = evt.Reason == DeliveryFailReason.Timeout ? FailTimeoutDelta : FailDefaultDelta;
            ApplyDelta(delta);
        }

        private void ApplyDelta(float delta)
        {
            float next = Mathf.Clamp(_currentRating + delta, MinRating, MaxRating);
            float appliedDelta = next - _currentRating;

            if (Mathf.Approximately(appliedDelta, 0f))
            {
                return;
            }

            SetRating(next, appliedDelta);

            if (_currentRating <= MinRating && !_depletedPublished)
            {
                _depletedPublished = true;
                Events.Publish(new RatingDepleted());
            }
        }

        private void SetRating(float next, float delta)
        {
            _currentRating = next;
            Events.Publish(new RatingChanged
            {
                Value = _currentRating,
                Delta = delta
            });
        }
    }
}
