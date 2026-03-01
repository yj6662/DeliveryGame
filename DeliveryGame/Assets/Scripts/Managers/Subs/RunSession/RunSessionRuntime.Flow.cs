using DeliveryRun;
using DeliveryRun.Delivery.RunSession;
using DeliveryRun.Managers.Core;
using UnityEngine;
using DomainRunChoicePointReached = DeliveryRun.Delivery.RunSession.RunChoicePointReached;
using DomainRunEndReason = DeliveryRun.Delivery.RunSession.RunEndReason;
using DomainRunLastOrderStarted = DeliveryRun.Delivery.RunSession.RunLastOrderStarted;
using DomainRunSession = DeliveryRun.Delivery.RunSession.RunSession;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionState = DeliveryRun.Delivery.RunSession.RunSessionState;
using DomainRunSessionStateChanged = DeliveryRun.Delivery.RunSession.RunSessionStateChanged;
using DomainRunTimerTicked = DeliveryRun.Delivery.RunSession.RunTimerTicked;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class RunSessionRuntime
    {
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

                _events.Publish(new DomainRunLastOrderStarted
                {
                    AtElapsedSeconds = LastOrderStartSecondsConst
                });
            }
        }

        private void PublishChoicePointReached(int index, float atElapsedSeconds)
        {
            _events.Publish(new DomainRunChoicePointReached
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

            _events.Publish(new DomainRunTimerTicked
            {
                ElapsedSeconds = _session.ElapsedSeconds,
                RemainingSeconds = _session.RemainingSeconds
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

            _events.Publish(new DomainRunSessionEnded
            {
                Reason = reason,
                ElapsedSeconds = _session.ElapsedSeconds
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
            _events.Publish(new ReturnToLobbyRequested());
            _session = null;
        }

        private void PublishStateChanged(DomainRunSessionState fromState, DomainRunSessionState toState)
        {
            _events.Publish(new DomainRunSessionStateChanged
            {
                From = fromState,
                To = toState
            });
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
                if (_uiManager == null)
                {
                    _services.TryGet(out _uiManager);
                }

                expectedPause = _uiManager != null && _uiManager.IsPauseMenuOpen;
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
