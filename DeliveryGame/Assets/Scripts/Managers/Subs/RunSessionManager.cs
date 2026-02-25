using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunSessionManager : SubManagerBase
    {
        public const float DefaultRunDurationSeconds = 420f;
        public const float DefaultLastOrderStartSeconds = 360f;

        private bool _isActive;
        private bool _lastOrderStarted;
        private int _runSequence;
        private int _lastPublishedWholeSecond;
        private float _elapsedSeconds;

        public override string Name => nameof(RunSessionManager);
        public override int InitOrder => 32;

        public bool IsActive => _isActive;
        public float RunDurationSeconds => DefaultRunDurationSeconds;
        public float LastOrderStartSeconds => DefaultLastOrderStartSeconds;
        public float ElapsedSeconds => _elapsedSeconds;
        public float RemainingSeconds => Mathf.Max(0f, DefaultRunDurationSeconds - _elapsedSeconds);
        public bool IsLastOrderPhase => _isActive && _elapsedSeconds >= DefaultLastOrderStartSeconds;

        protected override void OnInitialize()
        {
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<RatingDepleted>(Events, OnRatingDepleted);
            ResetState(keepRunSequence: true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            float dt = unscaledDeltaTime;
            if (dt < 0f)
            {
                dt = 0f;
            }

            _elapsedSeconds += dt;

            if (!_lastOrderStarted && _elapsedSeconds >= DefaultLastOrderStartSeconds)
            {
                _lastOrderStarted = true;
                Events.Publish(new RunSessionLastOrderStarted
                {
                    RunSequence = _runSequence,
                    ElapsedSeconds = _elapsedSeconds
                });
            }

            PublishTickIfNeeded();

            if (_elapsedSeconds >= DefaultRunDurationSeconds)
            {
                EndRunSession(RunEndReason.TimeUp, requestReturnToLobby: true);
            }
        }

        protected override void OnShutdown()
        {
            ResetState(keepRunSequence: false);
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName == SceneNames.RunScene)
            {
                BeginRunSession();
                return;
            }

            if (evt.SceneName == SceneNames.LobbyScene && _isActive)
            {
                EndRunSession(RunEndReason.SceneLeft, requestReturnToLobby: false);
            }
        }

        private void OnRatingDepleted(RatingDepleted evt)
        {
            if (!_isActive)
            {
                return;
            }

            EndRunSession(RunEndReason.RatingDepleted, requestReturnToLobby: true);
        }

        private void BeginRunSession()
        {
            _runSequence++;
            _isActive = true;
            _elapsedSeconds = 0f;
            _lastOrderStarted = false;
            _lastPublishedWholeSecond = -1;

            Events.Publish(new RunSessionStarted
            {
                RunSequence = _runSequence,
                DurationSeconds = DefaultRunDurationSeconds
            });

            PublishTickIfNeeded();
        }

        private void EndRunSession(RunEndReason reason, bool requestReturnToLobby)
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            Events.Publish(new RunSessionEnded
            {
                RunSequence = _runSequence,
                Reason = reason
            });

            if (requestReturnToLobby)
            {
                Events.Publish(new ReturnToLobbyRequested());
            }

            _elapsedSeconds = 0f;
            _lastOrderStarted = false;
            _lastPublishedWholeSecond = -1;
        }

        private void PublishTickIfNeeded()
        {
            int wholeSecond = Mathf.FloorToInt(_elapsedSeconds);
            if (wholeSecond == _lastPublishedWholeSecond)
            {
                return;
            }

            _lastPublishedWholeSecond = wholeSecond;
            Events.Publish(new RunSessionTick
            {
                RunSequence = _runSequence,
                ElapsedSeconds = _elapsedSeconds,
                RemainingSeconds = Mathf.Max(0f, DefaultRunDurationSeconds - _elapsedSeconds),
                IsLastOrderPhase = _lastOrderStarted || _elapsedSeconds >= DefaultLastOrderStartSeconds
            });
        }

        private void ResetState(bool keepRunSequence)
        {
            _isActive = false;
            _elapsedSeconds = 0f;
            _lastOrderStarted = false;
            _lastPublishedWholeSecond = -1;

            if (!keepRunSequence)
            {
                _runSequence = 0;
            }
        }
    }
}
