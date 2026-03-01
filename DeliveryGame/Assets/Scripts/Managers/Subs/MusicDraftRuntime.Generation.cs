using System;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class MusicDraftRuntime
    {
        private void GenerateDraft()
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
            if (_tracks.Length == 0)
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
            if (_tracks.Length == 0)
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
            if (_stack == null && _services != null)
            {
                _services.TryGet(out _stack);
            }

            float highTierMul = 1f;
            if (_stack != null)
            {
                highTierMul = _stack.GetMul(RunStatId.MusicHighTierChanceMultiplier);
            }

            highTierMul = Mathf.Clamp(highTierMul, 0.2f, 3f);
            int rareWeight = Mathf.Clamp(Mathf.RoundToInt(RareWeight * highTierMul), 4, 70);
            int epicWeight = Mathf.Clamp(Mathf.RoundToInt(EpicWeight * highTierMul), 1, 25);
            int commonWeight = 100 - rareWeight - epicWeight;
            if (commonWeight < 5)
            {
                commonWeight = 5;
                int highTotal = 100 - commonWeight;
                int totalHighBase = rareWeight + epicWeight;
                if (totalHighBase > 0)
                {
                    rareWeight = Mathf.RoundToInt((rareWeight / (float)totalHighBase) * highTotal);
                    epicWeight = highTotal - rareWeight;
                }
            }

            int roll = _rng.Next(100);
            if (roll < commonWeight)
            {
                return MusicTier.Common;
            }

            if (roll < commonWeight + rareWeight)
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
    }
}
