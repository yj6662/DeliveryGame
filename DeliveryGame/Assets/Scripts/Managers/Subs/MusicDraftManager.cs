using System;
using DeliveryRun.Delivery.RunSession;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;
using DomainRunChoicePointReached = DeliveryRun.Delivery.RunSession.RunChoicePointReached;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class MusicDraftManager : SubManagerBase
    {
        private const int ChoiceCount = 3;
        private const int CommonWeight = 70;
        private const int RareWeight = 25;

        private readonly string[] _pickedTrackIds = new string[ChoiceCount];
        private readonly string[] _draftTrackIds = new string[ChoiceCount];
        private readonly string[] _draftGenreIds = new string[ChoiceCount];

        private MusicDatabaseSO _database;
        private MusicLibraryService _library;
        private MusicTrackSO[] _tracks;
        private System.Random _rng;

        private int[] _commonTrackIndices;
        private int[] _rareTrackIndices;
        private int[] _epicTrackIndices;
        private int _commonTrackCount;
        private int _rareTrackCount;
        private int _epicTrackCount;

        private bool _isEnabled;
        private bool _runActive;

        public override string Name => nameof(MusicDraftManager);
        public override int InitOrder => 34;

        protected override void OnInitialize()
        {
            _database = Resources.Load<MusicDatabaseSO>("Bootstrap/MusicDatabase");
            if (_database == null)
            {
                Debug.LogError("[MusicDraftManager] Missing MusicDatabase at Resources/Bootstrap/MusicDatabase.");
                return;
            }

            _tracks = _database.Tracks != null ? _database.Tracks : Array.Empty<MusicTrackSO>();
            if (_tracks.Length == 0)
            {
                Debug.LogError("[MusicDraftManager] MusicDatabase has no tracks.");
                return;
            }

            _library = new MusicLibraryService(_database);
            Services.Register(_library);

            BuildTierIndices();
            _rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));
            _isEnabled = true;
            _runActive = false;
            ClearPickedTracks();
            ClearDraftTracks();

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<DomainRunChoicePointReached>(Events, OnChoicePointReached);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            if (!_isEnabled)
            {
                return;
            }

            _runActive = true;
            _rng = new System.Random(unchecked((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF)));
            ClearPickedTracks();
            ClearDraftTracks();
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
            ClearPickedTracks();
            ClearDraftTracks();
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            if (!_isEnabled)
            {
                return;
            }

            if (evt.ChoiceIndex < 0 || evt.ChoiceIndex >= ChoiceCount)
            {
                return;
            }

            _pickedTrackIds[evt.ChoiceIndex] = evt.TrackId;
        }

        private void OnChoicePointReached(DomainRunChoicePointReached evt)
        {
            if (!_isEnabled || !_runActive)
            {
                return;
            }

            if (evt.Index < 0 || evt.Index >= ChoiceCount)
            {
                return;
            }

            GenerateDraft(evt.Index);
            Events.Publish(new MusicDraftGenerated
            {
                ChoiceIndex = evt.Index,
                TrackId0 = _draftTrackIds[0],
                TrackId1 = _draftTrackIds[1],
                TrackId2 = _draftTrackIds[2]
            });
        }

        private void GenerateDraft(int choiceIndex)
        {
            ClearDraftTracks();

            for (int slot = 0; slot < ChoiceCount; slot++)
            {
                MusicTrackSO selected;
                if (!TrySelectTrack(slot, true, true, out selected) &&
                    !TrySelectTrack(slot, false, true, out selected) &&
                    !TrySelectTrack(slot, true, false, out selected) &&
                    !TrySelectTrack(slot, false, false, out selected))
                {
                    selected = SelectAnyUnused(slot);
                }

                if (selected == null)
                {
                    continue;
                }

                _draftTrackIds[slot] = selected.TrackId;
                _draftGenreIds[slot] = selected.Genre != null ? selected.Genre.GenreId : string.Empty;
            }
        }

        private bool TrySelectTrack(int slot, bool requireUniqueGenre, bool avoidPickedTracks, out MusicTrackSO track)
        {
            track = null;
            if (_tracks == null || _tracks.Length == 0)
            {
                return false;
            }

            int maxAttempts = _tracks.Length * 4;
            if (maxAttempts < 16)
            {
                maxAttempts = 16;
            }

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                MusicTier tier = RollTier();
                MusicTrackSO candidate = SelectTrackByTier(tier);
                if (candidate == null || string.IsNullOrEmpty(candidate.TrackId))
                {
                    continue;
                }

                if (IsTrackUsedInDraft(slot, candidate.TrackId))
                {
                    continue;
                }

                if (avoidPickedTracks && IsTrackAlreadyPicked(candidate.TrackId))
                {
                    continue;
                }

                string genreId = candidate.Genre != null ? candidate.Genre.GenreId : string.Empty;
                if (requireUniqueGenre && IsGenreUsedInDraft(slot, genreId))
                {
                    continue;
                }

                track = candidate;
                return true;
            }

            return false;
        }

        private MusicTrackSO SelectAnyUnused(int slot)
        {
            if (_tracks == null || _tracks.Length == 0)
            {
                return null;
            }

            int startIndex = _rng.Next(_tracks.Length);
            for (int i = 0; i < _tracks.Length; i++)
            {
                int idx = startIndex + i;
                if (idx >= _tracks.Length)
                {
                    idx -= _tracks.Length;
                }

                MusicTrackSO candidate = _tracks[idx];
                if (candidate == null || string.IsNullOrEmpty(candidate.TrackId))
                {
                    continue;
                }

                if (IsTrackUsedInDraft(slot, candidate.TrackId))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private MusicTrackSO SelectTrackByTier(MusicTier tier)
        {
            int[] indices;
            int count;

            if (tier == MusicTier.Common)
            {
                indices = _commonTrackIndices;
                count = _commonTrackCount;
            }
            else if (tier == MusicTier.Rare)
            {
                indices = _rareTrackIndices;
                count = _rareTrackCount;
            }
            else
            {
                indices = _epicTrackIndices;
                count = _epicTrackCount;
            }

            if (indices == null || count <= 0)
            {
                return null;
            }

            int selected = _rng.Next(count);
            int trackIndex = indices[selected];
            if (trackIndex < 0 || trackIndex >= _tracks.Length)
            {
                return null;
            }

            return _tracks[trackIndex];
        }

        private MusicTier RollTier()
        {
            int roll = _rng.Next(100);
            if (roll < CommonWeight)
            {
                return MusicTier.Common;
            }

            if (roll < CommonWeight + RareWeight)
            {
                return MusicTier.Rare;
            }

            return MusicTier.Epic;
        }

        private bool IsTrackUsedInDraft(int slot, string trackId)
        {
            if (string.IsNullOrEmpty(trackId))
            {
                return false;
            }

            for (int i = 0; i < slot; i++)
            {
                if (string.Equals(_draftTrackIds[i], trackId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGenreUsedInDraft(int slot, string genreId)
        {
            if (string.IsNullOrEmpty(genreId))
            {
                return false;
            }

            for (int i = 0; i < slot; i++)
            {
                if (string.Equals(_draftGenreIds[i], genreId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsTrackAlreadyPicked(string trackId)
        {
            if (string.IsNullOrEmpty(trackId))
            {
                return false;
            }

            for (int i = 0; i < ChoiceCount; i++)
            {
                if (string.Equals(_pickedTrackIds[i], trackId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildTierIndices()
        {
            _commonTrackIndices = new int[_tracks.Length];
            _rareTrackIndices = new int[_tracks.Length];
            _epicTrackIndices = new int[_tracks.Length];
            _commonTrackCount = 0;
            _rareTrackCount = 0;
            _epicTrackCount = 0;

            for (int i = 0; i < _tracks.Length; i++)
            {
                MusicTrackSO track = _tracks[i];
                if (track == null)
                {
                    continue;
                }

                if (track.Tier == MusicTier.Common)
                {
                    _commonTrackIndices[_commonTrackCount] = i;
                    _commonTrackCount++;
                }
                else if (track.Tier == MusicTier.Rare)
                {
                    _rareTrackIndices[_rareTrackCount] = i;
                    _rareTrackCount++;
                }
                else
                {
                    _epicTrackIndices[_epicTrackCount] = i;
                    _epicTrackCount++;
                }
            }
        }

        private void ClearPickedTracks()
        {
            for (int i = 0; i < _pickedTrackIds.Length; i++)
            {
                _pickedTrackIds[i] = null;
            }
        }

        private void ClearDraftTracks()
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                _draftTrackIds[i] = null;
                _draftGenreIds[i] = null;
            }
        }
    }
}
