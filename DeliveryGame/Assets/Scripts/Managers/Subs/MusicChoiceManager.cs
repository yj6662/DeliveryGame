using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    public sealed class MusicChoiceManager : SubManagerBase
    {
        private const int ChoiceCount = 3;
        private const float ChoiceTime0 = 0f;
        private const float ChoiceTime1 = 180f;
        private const float ChoiceTime2 = 360f;
        private const float DecisionTimeLimitSeconds = 8f;

        private bool _runActive;
        private bool _pendingChoice;
        private int _runSequence;
        private int _nextChoiceIndex;
        private int _pendingChoiceIndex;
        private float _elapsedSeconds;
        private float _pendingDeadlineSeconds;

        public override string Name => nameof(MusicChoiceManager);
        public override int InitOrder => 37;

        public bool IsRunActive => _runActive;
        public bool HasPendingChoice => _pendingChoice;
        public int PendingChoiceIndex => _pendingChoice ? _pendingChoiceIndex : -1;
        public float PendingChoiceRemainingSeconds =>
            _pendingChoice ? Mathf.Max(0f, _pendingDeadlineSeconds - _elapsedSeconds) : 0f;

        protected override void OnInitialize()
        {
            ResetState(keepRunSequence: true);
            Subs.Add<RunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<RunSessionEnded>(Events, OnRunSessionEnded);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!_runActive)
            {
                return;
            }

            float dt = unscaledDeltaTime;
            if (dt < 0f)
            {
                dt = 0f;
            }

            _elapsedSeconds += dt;

            if (_pendingChoice)
            {
                if (_elapsedSeconds >= _pendingDeadlineSeconds)
                {
                    AutoResolvePendingChoice();
                }

                return;
            }

            TryRequestDueChoice();
        }

        protected override void OnShutdown()
        {
            ResetState(keepRunSequence: false);
        }

        public void ApplyChoice(string modifierId)
        {
            if (!_runActive || !_pendingChoice)
            {
                return;
            }

            if (string.IsNullOrEmpty(modifierId))
            {
                modifierId = "mod/default";
            }

            PublishChoiceApplied(modifierId, autoSelected: false);
            AdvanceChoiceState();
        }

        private void OnRunSessionStarted(RunSessionStarted evt)
        {
            _runActive = true;
            _runSequence = evt.RunSequence;
            _elapsedSeconds = 0f;
            _nextChoiceIndex = 0;
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _pendingDeadlineSeconds = 0f;
            TryRequestDueChoice();
        }

        private void OnRunSessionEnded(RunSessionEnded evt)
        {
            _runActive = false;
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _pendingDeadlineSeconds = 0f;
            _elapsedSeconds = 0f;
            _nextChoiceIndex = ChoiceCount;
        }

        private void TryRequestDueChoice()
        {
            if (_pendingChoice || _nextChoiceIndex >= ChoiceCount)
            {
                return;
            }

            float dueTime = GetChoiceDueTime(_nextChoiceIndex);
            if (_elapsedSeconds < dueTime)
            {
                return;
            }

            RequestChoice(_nextChoiceIndex);
        }

        private void RequestChoice(int choiceIndex)
        {
            _pendingChoice = true;
            _pendingChoiceIndex = choiceIndex;
            _pendingDeadlineSeconds = _elapsedSeconds + DecisionTimeLimitSeconds;

            Events.Publish(new MusicChoiceRequested
            {
                RunSequence = _runSequence,
                ChoiceIndex = choiceIndex,
                ElapsedSeconds = _elapsedSeconds,
                DecisionTimeLimitSeconds = DecisionTimeLimitSeconds
            });
        }

        private void AutoResolvePendingChoice()
        {
            if (!_pendingChoice)
            {
                return;
            }

            string modifierId = "mod/auto_" + _pendingChoiceIndex;
            Events.Publish(new MusicChoiceAutoResolved
            {
                RunSequence = _runSequence,
                ChoiceIndex = _pendingChoiceIndex,
                ModifierId = modifierId
            });

            PublishChoiceApplied(modifierId, autoSelected: true);
            AdvanceChoiceState();
        }

        private void PublishChoiceApplied(string modifierId, bool autoSelected)
        {
            Events.Publish(new MusicChoiceApplied
            {
                RunSequence = _runSequence,
                ChoiceIndex = _pendingChoiceIndex,
                ModifierId = modifierId,
                AutoSelected = autoSelected
            });
        }

        private void AdvanceChoiceState()
        {
            _pendingChoice = false;
            _nextChoiceIndex = _pendingChoiceIndex + 1;
            _pendingChoiceIndex = -1;
            _pendingDeadlineSeconds = 0f;
        }

        private static float GetChoiceDueTime(int choiceIndex)
        {
            if (choiceIndex == 0)
            {
                return ChoiceTime0;
            }

            if (choiceIndex == 1)
            {
                return ChoiceTime1;
            }

            return ChoiceTime2;
        }

        private void ResetState(bool keepRunSequence)
        {
            _runActive = false;
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _pendingDeadlineSeconds = 0f;
            _elapsedSeconds = 0f;
            _nextChoiceIndex = ChoiceCount;

            if (!keepRunSequence)
            {
                _runSequence = 0;
            }
        }
    }
}
