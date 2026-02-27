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
using LegacyRunEndReason = DeliveryRun.Managers.Core.RunEndReason;
using LegacyRunSessionEnded = DeliveryRun.Managers.Core.RunSessionEnded;
using LegacyRunSessionLastOrderStarted = DeliveryRun.Managers.Core.RunSessionLastOrderStarted;
using LegacyRunSessionStarted = DeliveryRun.Managers.Core.RunSessionStarted;
using LegacyRunSessionTick = DeliveryRun.Managers.Core.RunSessionTick;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunSessionManager : SubManagerBase
    {
        private const float RunDurationSecondsConst = 420f;
        private const float LastOrderStartSecondsConst = 360f;
        private const float TickPublishIntervalSeconds = 0.1f;
        private static readonly float[] ChoiceTimesSeconds = { 0f, 180f, 300f };

        private DomainRunSession _session;
        private float _tickPublishAccum;
        private bool _returnRequested;
        private float _savedTimeScale;
        private bool _timeScaleCaptured;
        private bool _endedPublished;
        private bool _lastOrderStarted;
        private int _runSequence;
        private float _unexpectedPauseAccum;
        private bool _unexpectedPauseLogged;
        private UiRunHudManager _uiRunHudManager;

        public override string Name => nameof(RunSessionManager);
        public override int InitOrder => 40;

        public DomainRunSessionState CurrentState => _session != null ? _session.State : DomainRunSessionState.Ready;
        public float RemainingSeconds => _session != null ? _session.RemainingSeconds : 0f;
        public bool HasActiveRun => _session != null && _session.State != DomainRunSessionState.Ended;

        // Legacy-accessors kept to avoid churn in existing debug/UI callers.
        public bool IsActive => HasActiveRun;
        public float ElapsedSeconds => _session != null ? _session.ElapsedSeconds : 0f;
        public float RunDurationSeconds => RunDurationSecondsConst;
        public bool IsLastOrderPhase => _lastOrderStarted;

        protected override void OnInitialize()
        {
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<RatingZeroReached>(Events, OnRatingZeroReached);
            Subs.Add<RatingDepleted>(Events, OnRatingDepleted);
            Subs.Add<FuelDepleted>(Events, OnFuelDepleted);

            if (SceneManager.GetActiveScene().name == SceneNames.RunScene)
            {
                BeginNewRun();
            }
        }

        protected override void OnTick(float unscaledDeltaTime)
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

        protected override void OnShutdown()
        {
            CleanupIfNeeded();
            RestoreCapturedTimeScale();
        }

        public void ForceEndRun()
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

        public void DebugAddElapsed(float seconds)
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

        public void DebugEnterChoicePause()
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

        public void DebugResumeFromChoice()
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

        public void EnterPauseForChoice()
        {
            DebugEnterChoicePause();
        }

        public void ResumeFromChoice()
        {
            DebugResumeFromChoice();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName == SceneNames.RunScene)
            {
                BeginNewRun();
                return;
            }

            CleanupIfNeeded();
        }

        private void OnRatingZeroReached(RatingZeroReached evt)
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

        private void OnRatingDepleted(RatingDepleted evt)
        {
            OnRatingZeroReached(new RatingZeroReached { Rating = 0f });
        }

        private void OnFuelDepleted(FuelDepleted evt)
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

            _runSequence++;
            _session = new DomainRunSession(RunDurationSecondsConst, ChoiceTimesSeconds, LastOrderStartSecondsConst);

            if (_session.Start())
            {
                PublishStateChanged(DomainRunSessionState.Ready, DomainRunSessionState.Running);
            }

            Events.Publish(new DomainRunSessionStarted
            {
                DurationSeconds = RunDurationSecondsConst
            });

            Events.Publish(new LegacyRunSessionStarted
            {
                RunSequence = _runSequence,
                DurationSeconds = RunDurationSecondsConst
            });

            int flags = _session.Tick(0f);
            HandleFlags(flags);
            PublishTimerTicked();
        }

        private void CleanupIfNeeded()
        {
            if (_session != null)
            {
                if (_session.State != DomainRunSessionState.Ended)
                {
                    DomainRunSessionState fromState = _session.State;
                    if (_session.End(DomainRunEndReason.Forced))
                    {
                        PublishStateChanged(fromState, _session.State);
                    }
                }

                PublishEndedIfNeeded();
            }

            _session = null;
            _tickPublishAccum = 0f;
            _returnRequested = false;
            _endedPublished = false;
            _lastOrderStarted = false;
            _unexpectedPauseAccum = 0f;
            _unexpectedPauseLogged = false;
            RestoreCapturedTimeScale();
        }

        private void HandleFlags(int flags)
        {
            if (_session == null || flags == 0)
            {
                return;
            }

            if ((flags & DomainRunSession.Choice0) != 0)
            {
                PublishChoicePointReached(0, 0f);
            }

            if ((flags & DomainRunSession.Choice1) != 0)
            {
                PublishChoicePointReached(1, 180f);
            }

            if ((flags & DomainRunSession.Choice2) != 0)
            {
                PublishChoicePointReached(2, 300f);
            }

            if ((flags & DomainRunSession.LastOrder) != 0)
            {
                _lastOrderStarted = true;

                Events.Publish(new DomainRunLastOrderStarted
                {
                    AtElapsedSeconds = LastOrderStartSecondsConst
                });

                Events.Publish(new LegacyRunSessionLastOrderStarted
                {
                    RunSequence = _runSequence,
                    ElapsedSeconds = _session.ElapsedSeconds
                });
            }
        }

        private void PublishChoicePointReached(int index, float atElapsedSeconds)
        {
            Events.Publish(new DomainRunChoicePointReached
            {
                Index = index,
                AtElapsedSeconds = atElapsedSeconds
            });
        }

        private void PublishTimerTicked()
        {
            if (_session == null)
            {
                return;
            }

            Events.Publish(new DomainRunTimerTicked
            {
                ElapsedSeconds = _session.ElapsedSeconds,
                RemainingSeconds = _session.RemainingSeconds
            });

            Events.Publish(new LegacyRunSessionTick
            {
                RunSequence = _runSequence,
                ElapsedSeconds = _session.ElapsedSeconds,
                RemainingSeconds = _session.RemainingSeconds,
                IsLastOrderPhase = _lastOrderStarted
            });
        }

        private void PublishEndedIfNeeded()
        {
            if (_session == null || _endedPublished)
            {
                return;
            }

            DomainRunEndReason reason = _session.EndReason.HasValue
                ? _session.EndReason.Value
                : DomainRunEndReason.Forced;

            Events.Publish(new DomainRunSessionEnded
            {
                Reason = reason,
                ElapsedSeconds = _session.ElapsedSeconds
            });

            Events.Publish(new LegacyRunSessionEnded
            {
                RunSequence = _runSequence,
                Reason = ToLegacyEndReason(reason)
            });

            _endedPublished = true;
        }

        private void RequestReturnToLobbyOnce()
        {
            RestoreCapturedTimeScale();
            if (_returnRequested)
            {
                return;
            }

            _returnRequested = true;
            Events.Publish(new ReturnToLobbyRequested());
            _session = null;
        }

        private void PublishStateChanged(DomainRunSessionState fromState, DomainRunSessionState toState)
        {
            Events.Publish(new DomainRunSessionStateChanged
            {
                From = fromState,
                To = toState
            });
        }

        private static LegacyRunEndReason ToLegacyEndReason(DomainRunEndReason reason)
        {
            if (reason == DomainRunEndReason.TimeExpired)
            {
                return LegacyRunEndReason.TimeUp;
            }

            if (reason == DomainRunEndReason.RatingZero)
            {
                return LegacyRunEndReason.RatingDepleted;
            }

            if (reason == DomainRunEndReason.OutOfFuel)
            {
                return LegacyRunEndReason.FuelDepleted;
            }

            return LegacyRunEndReason.SceneLeft;
        }

        private void CaptureTimeScaleIfNeeded()
        {
            if (_timeScaleCaptured)
            {
                return;
            }

            _savedTimeScale = Time.timeScale;
            _timeScaleCaptured = true;
        }

        private void RestoreCapturedTimeScale()
        {
            if (!_timeScaleCaptured)
            {
                return;
            }

            Time.timeScale = _savedTimeScale;
            _savedTimeScale = 0f;
            _timeScaleCaptured = false;
        }

        private void RecoverUnexpectedPausedTimescale(float dt)
        {
            if (Time.timeScale > 0.0001f)
            {
                _unexpectedPauseAccum = 0f;
                _unexpectedPauseLogged = false;
                return;
            }

            bool expectedPause = _session != null && _session.State == DomainRunSessionState.PauseForChoice;
            if (!expectedPause)
            {
                if (_uiRunHudManager == null)
                {
                    Services.TryGet(out _uiRunHudManager);
                }

                expectedPause = _uiRunHudManager != null && _uiRunHudManager.IsPauseMenuOpen;
            }

            if (expectedPause)
            {
                _unexpectedPauseAccum = 0f;
                _unexpectedPauseLogged = false;
                return;
            }

            _unexpectedPauseAccum += dt;
            if (_unexpectedPauseAccum < 0.45f)
            {
                return;
            }

            Time.timeScale = 1f;
            _unexpectedPauseAccum = 0f;
            if (_unexpectedPauseLogged)
            {
                return;
            }

            _unexpectedPauseLogged = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[RunSessionManager] Detected unexpected paused timescale. Auto-restored to 1.");
#endif
        }
    }
}
