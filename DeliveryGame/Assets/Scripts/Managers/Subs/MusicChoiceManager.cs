using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    public sealed class MusicChoiceManager : SubManagerBase
    {
        private const int ChoiceCount = 3;
        private const float DecisionTimeLimitSeconds = 8f;

        private readonly string[] _pendingDraftTrackIds = new string[ChoiceCount];

        private MusicLibraryService _library;
        private bool _runActive;
        private bool _pendingChoice;
        private bool _ignoreSelectedEvent;
        private int _runSequence;
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
            Services.TryGet(out _library);

            Subs.Add<RunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<RunSessionEnded>(Events, OnRunSessionEnded);
            Subs.Add<MusicDraftGenerated>(Events, OnMusicDraftGenerated);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
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
            }
        }

        protected override void OnShutdown()
        {
            ResetState(keepRunSequence: false);
        }

        public void ApplyChoice(string trackId)
        {
            if (!_runActive || !_pendingChoice)
            {
                return;
            }

            int optionIndex = ResolveOptionIndex(trackId);
            if (optionIndex < 0)
            {
                optionIndex = ResolveAutoOptionIndex();
            }

            string resolvedTrackId = ResolveTrackId(optionIndex, trackId);
            string genreId = ResolveGenreId(resolvedTrackId);

            Events.Publish(new MusicChoiceSelected
            {
                ChoiceIndex = _pendingChoiceIndex,
                OptionIndex = optionIndex,
                TrackId = resolvedTrackId,
                GenreId = genreId
            });
        }

        private void OnRunSessionStarted(RunSessionStarted evt)
        {
            _runActive = true;
            _runSequence = evt.RunSequence;
            _elapsedSeconds = 0f;
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _pendingDeadlineSeconds = 0f;
            _ignoreSelectedEvent = false;
            ClearPendingDraft();
        }

        private void OnRunSessionEnded(RunSessionEnded evt)
        {
            _runActive = false;
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _pendingDeadlineSeconds = 0f;
            _elapsedSeconds = 0f;
            _ignoreSelectedEvent = false;
            ClearPendingDraft();
        }

        private void OnMusicDraftGenerated(MusicDraftGenerated evt)
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

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            if (_ignoreSelectedEvent || !_runActive || !_pendingChoice)
            {
                return;
            }

            if (evt.ChoiceIndex != _pendingChoiceIndex)
            {
                return;
            }

            int optionIndex = NormalizeOptionIndex(evt.OptionIndex);
            string trackId = ResolveTrackId(optionIndex, evt.TrackId);
            string genreId = string.IsNullOrEmpty(evt.GenreId) ? ResolveGenreId(trackId) : evt.GenreId;

            PublishChoiceApplied(trackId, optionIndex, genreId, autoSelected: false);
            AdvanceChoiceState();
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
            if (!_runActive || !_pendingChoice)
            {
                return;
            }

            int optionIndex = ResolveAutoOptionIndex();
            string trackId = ResolveTrackId(optionIndex, null);
            string genreId = ResolveGenreId(trackId);

            Events.Publish(new MusicChoiceAutoResolved
            {
                RunSequence = _runSequence,
                ChoiceIndex = _pendingChoiceIndex,
                ModifierId = trackId,
                OptionIndex = optionIndex,
                TrackId = trackId,
                GenreId = genreId
            });

            _ignoreSelectedEvent = true;
            try
            {
                Events.Publish(new MusicChoiceSelected
                {
                    ChoiceIndex = _pendingChoiceIndex,
                    OptionIndex = optionIndex,
                    TrackId = trackId,
                    GenreId = genreId
                });
            }
            finally
            {
                _ignoreSelectedEvent = false;
            }

            PublishChoiceApplied(trackId, optionIndex, genreId, autoSelected: true);
            AdvanceChoiceState();
        }

        private void PublishChoiceApplied(string trackId, int optionIndex, string genreId, bool autoSelected)
        {
            Events.Publish(new MusicChoiceApplied
            {
                RunSequence = _runSequence,
                ChoiceIndex = _pendingChoiceIndex,
                ModifierId = trackId,
                OptionIndex = optionIndex,
                TrackId = trackId,
                GenreId = genreId,
                AutoSelected = autoSelected
            });
        }

        private void AdvanceChoiceState()
        {
            _pendingChoice = false;
            _pendingChoiceIndex = -1;
            _pendingDeadlineSeconds = 0f;
            ClearPendingDraft();
        }

        private int ResolveOptionIndex(string trackId)
        {
            if (string.IsNullOrEmpty(trackId))
            {
                return -1;
            }

            for (int i = 0; i < ChoiceCount; i++)
            {
                if (string.Equals(_pendingDraftTrackIds[i], trackId, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int NormalizeOptionIndex(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex >= ChoiceCount)
            {
                return 0;
            }

            return optionIndex;
        }

        private int ResolveAutoOptionIndex()
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                if (!string.IsNullOrEmpty(_pendingDraftTrackIds[i]))
                {
                    return i;
                }
            }

            return 0;
        }

        private string ResolveTrackId(int optionIndex, string preferredTrackId)
        {
            if (!string.IsNullOrEmpty(preferredTrackId))
            {
                return preferredTrackId;
            }

            int normalized = NormalizeOptionIndex(optionIndex);
            return _pendingDraftTrackIds[normalized];
        }

        private string ResolveGenreId(string trackId)
        {
            if (string.IsNullOrEmpty(trackId))
            {
                return string.Empty;
            }

            if (_library == null)
            {
                Services.TryGet(out _library);
            }

            if (_library == null)
            {
                return string.Empty;
            }

            MusicTrackSO track;
            if (!_library.TryGetTrack(trackId, out track) || track == null || track.Genre == null)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(track.Genre.GenreId) ? string.Empty : track.Genre.GenreId;
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
            _pendingDeadlineSeconds = 0f;
            _elapsedSeconds = 0f;
            ClearPendingDraft();

            if (!keepRunSequence)
            {
                _runSequence = 0;
            }
        }
    }
}
