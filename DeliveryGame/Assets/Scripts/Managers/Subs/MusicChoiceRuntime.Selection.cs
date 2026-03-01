using DeliveryRun.Managers.Core;
using DeliveryRun.Music;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MusicChoiceRuntime
    {
        internal void OnMusicChoiceSelected(MusicChoiceSelected evt)
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

        internal void ApplyChoice(string trackId)
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

            _events.Publish(new MusicChoiceSelected
            {
                ChoiceIndex = _pendingChoiceIndex,
                OptionIndex = optionIndex,
                TrackId = resolvedTrackId,
                GenreId = genreId
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

            _events.Publish(new MusicChoiceAutoResolved
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
                _events.Publish(new MusicChoiceSelected
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
            _events.Publish(new MusicChoiceApplied
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
                _services.TryGet(out _library);
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
    }
}
