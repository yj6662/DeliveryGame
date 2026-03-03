using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;
using DomainRunChoiceConstants = DeliveryRun.Delivery.RunSession.RunChoiceConstants;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MusicChoiceRuntime
    {
        private const int ChoiceCount = DomainRunChoiceConstants.ChoiceCount;
        private const float DecisionTimeLimitSeconds = -1f;

        private readonly string[] _pendingDraftTrackIds = new string[ChoiceCount];
        private readonly ServiceRegistry _services;
        private readonly EventBus _events;

        private MusicLibraryService _library;
        private bool _runActive;
        private bool _pendingChoice;
        private bool _ignoreSelectedEvent;
        private int _runSequence;
        private int _pendingChoiceIndex;
        private float _elapsedSeconds;

        internal MusicChoiceRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal bool IsRunActive => _runActive;
        internal bool HasPendingChoice => _pendingChoice;
        internal int PendingChoiceIndex => _pendingChoice ? _pendingChoiceIndex : -1;
        internal float PendingChoiceRemainingSeconds => _pendingChoice ? -1f : 0f;

        internal void Initialize()
        {
            ResetState(keepRunSequence: true);
            _services.TryGet(out _library);
        }

        internal void Shutdown()
        {
            ResetState(keepRunSequence: false);
        }

        internal void Tick(float unscaledDeltaTime)
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
        }

        internal void OnRunSessionStarted()
        {
            _runActive = true;
            _runSequence++;
            _elapsedSeconds = 0f;
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _ignoreSelectedEvent = false;
            ClearPendingDraft();
        }

        internal void OnRunSessionEnded()
        {
            _runActive = false;
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _elapsedSeconds = 0f;
            _ignoreSelectedEvent = false;
            ClearPendingDraft();
        }

        internal void OnMusicDraftGenerated(MusicDraftGenerated evt)
        {
            if (!_runActive)
            {
                return;
            }

            if (evt.ChoiceIndex < 0 || evt.ChoiceIndex >= ChoiceCount)
            {
                return;
            }

            if (_pendingChoice && _pendingChoiceIndex != evt.ChoiceIndex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[MusicChoiceManager] Received draft for choice " + evt.ChoiceIndex +
                    " while choice " + _pendingChoiceIndex + " is pending. Ignored.");
#endif
                return;
            }

            CachePendingDraft(evt);

            if (_pendingChoice)
            {
                return;
            }

            RequestChoice(evt.ChoiceIndex);
        }

        private void RequestChoice(int choiceIndex)
        {
            _pendingChoice = true;
            _pendingChoiceIndex = choiceIndex;

            _events.Publish(new MusicChoiceRequested
            {
                RunSequence = _runSequence,
                ChoiceIndex = choiceIndex,
                ElapsedSeconds = _elapsedSeconds,
                DecisionTimeLimitSeconds = DecisionTimeLimitSeconds
            });
        }

        private void CachePendingDraft(MusicDraftGenerated evt)
        {
            _pendingDraftTrackIds[0] = evt.TrackId0;
            _pendingDraftTrackIds[1] = evt.TrackId1;
            _pendingDraftTrackIds[2] = evt.TrackId2;
        }

        private void ClearPendingDraft()
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                _pendingDraftTrackIds[i] = null;
            }
        }

        private void ResetState(bool keepRunSequence)
        {
            _runActive = false;
            _pendingChoice = false;
            _ignoreSelectedEvent = false;
            _pendingChoiceIndex = -1;
            _elapsedSeconds = 0f;
            ClearPendingDraft();

            if (!keepRunSequence)
            {
                _runSequence = 0;
            }
        }
    }
}
