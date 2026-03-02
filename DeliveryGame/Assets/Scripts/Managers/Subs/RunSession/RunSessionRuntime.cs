using DeliveryRun;
using DeliveryRun.Delivery.RunSession;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainRunChoicePointReached = DeliveryRun.Delivery.RunSession.RunChoicePointReached;
using DomainRunEndReason = DeliveryRun.Delivery.RunSession.RunEndReason;
using DomainRunLastOrderStarted = DeliveryRun.Delivery.RunSession.RunLastOrderStarted;
using DomainRunSession = DeliveryRun.Delivery.RunSession.RunSession;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;
using DomainRunSessionState = DeliveryRun.Delivery.RunSession.RunSessionState;
using DomainRunSessionStateChanged = DeliveryRun.Delivery.RunSession.RunSessionStateChanged;
using DomainRunTimerTicked = DeliveryRun.Delivery.RunSession.RunTimerTicked;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class RunSessionRuntime
    {
        private const float RunDurationSecondsConst = 420f;
        private const float LastOrderStartSecondsConst = 360f;
        private const float TickPublishIntervalSeconds = 0.1f;
        private static readonly float[] ChoiceTimesSeconds = { 0f, 180f, 360f };

        private readonly ServiceRegistry _services;
        private readonly EventBus _events;

        private DomainRunSession _session;
        private float _tickPublishAccum;
        private bool _returnRequested;
        private float _savedTimeScale;
        private bool _timeScaleCaptured;
        private bool _endedPublished;
        private bool _lastOrderStarted;
        private float _unexpectedPauseAccum;
        private bool _unexpectedPauseLogged;
        private UIManager _uiManager;

        internal RunSessionRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal DomainRunSessionState CurrentState => _session != null ? _session.State : DomainRunSessionState.Ready;
        internal float RemainingSeconds => _session != null ? _session.RemainingSeconds : 0f;
        internal bool HasActiveRun => _session != null && _session.State != DomainRunSessionState.Ended;
        internal float ElapsedSeconds => _session != null ? _session.ElapsedSeconds : 0f;
        internal float RunDurationSeconds => RunDurationSecondsConst;
        internal bool IsLastOrderPhase => _lastOrderStarted;

        internal void Initialize()
        {
            if (SceneManager.GetActiveScene().name == SceneNames.RunScene)
            {
                BeginNewRun();
            }
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (_session == null)
            {
                return;
            }

            float dt = unscaledDeltaTime;
            if (dt < 0f)
            {
                dt = 0f;
            }

            RecoverUnexpectedPausedTimescale(dt);

            DomainRunSessionState fromState = _session.State;
            int flags = _session.Tick(dt);
            if (_session != null && _session.State != fromState)
            {
                PublishStateChanged(fromState, _session.State);
            }

            HandleFlags(flags);

            _tickPublishAccum += dt;
            if (_tickPublishAccum >= TickPublishIntervalSeconds)
            {
                _tickPublishAccum = 0f;
                PublishTimerTicked();
            }

            if ((flags & DomainRunSession.TimeExpired) != 0)
            {
                PublishEndedIfNeeded();
                RequestReturnToLobbyOnce();
                return;
            }

            if (_session != null && _session.State == DomainRunSessionState.Ended)
            {
                PublishEndedIfNeeded();
                RequestReturnToLobbyOnce();
            }
        }

        internal void Shutdown()
        {
            CleanupIfNeeded();
            RestoreCapturedTimeScale();
        }

        internal void ForceEndRun()
        {
            if (_session == null)
            {
                return;
            }

            DomainRunSessionState fromState = _session.State;
            if (_session.End(DomainRunEndReason.Forced))
            {
                PublishStateChanged(fromState, _session.State);
            }
        }

        internal void DebugAddElapsed(float seconds)
        {
            if (_session == null || seconds <= 0f)
            {
                return;
            }

            DomainRunSessionState fromState = _session.State;
            int flags = _session.Tick(seconds);
            if (_session != null && _session.State != fromState)
            {
                PublishStateChanged(fromState, _session.State);
            }

            HandleFlags(flags);
            _tickPublishAccum = 0f;
            PublishTimerTicked();

            if ((flags & DomainRunSession.TimeExpired) != 0)
            {
                PublishEndedIfNeeded();
                RequestReturnToLobbyOnce();
                return;
            }

            if (_session != null && _session.State == DomainRunSessionState.Ended)
            {
                PublishEndedIfNeeded();
                RequestReturnToLobbyOnce();
            }
        }

        internal void DebugEnterChoicePause()
        {
            if (_session == null || _session.State != DomainRunSessionState.Running)
            {
                return;
            }

            DomainRunSessionState fromState = _session.State;
            CaptureTimeScaleIfNeeded();
            Time.timeScale = 0f;

            if (_session.EnterPauseForChoice())
            {
                PublishStateChanged(fromState, _session.State);
                return;
            }

            RestoreCapturedTimeScale();
        }

        internal void DebugResumeFromChoice()
        {
            if (_session == null)
            {
                return;
            }

            DomainRunSessionState fromState = _session.State;
            if (!_session.ResumeFromChoice())
            {
                return;
            }

            RestoreCapturedTimeScale();
            PublishStateChanged(fromState, _session.State);
        }

        internal void EnterPauseForChoice()
        {
            DebugEnterChoicePause();
        }

        internal void ResumeFromChoice()
        {
            DebugResumeFromChoice();
        }

        internal void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName == SceneNames.RunScene)
            {
                BeginNewRun();
                return;
            }

            CleanupIfNeeded();
        }

        internal void OnRatingZeroReached(RatingZeroReached evt)
        {
            if (_session == null || _session.State == DomainRunSessionState.Ended)
            {
                return;
            }

            DomainRunSessionState fromState = _session.State;
            if (_session.End(DomainRunEndReason.RatingZero))
            {
                PublishStateChanged(fromState, _session.State);
            }
        }

        internal void OnRatingDepleted(RatingDepleted evt)
        {
            OnRatingZeroReached(new RatingZeroReached { Rating = 0f });
        }

        internal void OnFuelDepleted(FuelDepleted evt)
        {
            if (_session == null || _session.State == DomainRunSessionState.Ended)
            {
                return;
            }

            DomainRunSessionState fromState = _session.State;
            if (_session.End(DomainRunEndReason.OutOfFuel))
            {
                PublishStateChanged(fromState, _session.State);
            }

            PublishEndedIfNeeded();
            RequestReturnToLobbyOnce();
        }

        private void BeginNewRun()
        {
            CleanupIfNeeded();

            _returnRequested = false;
            _tickPublishAccum = 0f;
            _endedPublished = false;
            _lastOrderStarted = false;
            _unexpectedPauseAccum = 0f;
            _unexpectedPauseLogged = false;

            _session = new DomainRunSession(RunDurationSecondsConst, ChoiceTimesSeconds, LastOrderStartSecondsConst);

            if (_session.Start())
            {
                PublishStateChanged(DomainRunSessionState.Ready, DomainRunSessionState.Running);
            }

            _events.Publish(new DomainRunSessionStarted
            {
                DurationSeconds = RunDurationSecondsConst
            });

            int flags = _session.Tick(0f);
            HandleFlags(flags);
            PublishTimerTicked();
        }
    }
}
