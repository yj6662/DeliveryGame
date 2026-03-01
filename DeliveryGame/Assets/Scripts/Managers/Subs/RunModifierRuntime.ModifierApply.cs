using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class RunModifierRuntime
    {
        private void ApplyTrackModifiers(int choiceIndex, MusicTrackSO track)
        {
            if (track == null || track.Modifiers == null)
            {
                return;
            }

            List<string> list = _trackSourceIdsByChoice[choiceIndex];
            for (int i = 0; i < track.Modifiers.Length; i++)
            {
                MusicModifierDef modifier = track.Modifiers[i];
                RunStatId stat;
                if (!TryMapStatKey(modifier.StatKey, out stat))
                {
                    continue;
                }

                ModifierMode mode = modifier.Mode == MusicModifierMode.Add
                    ? ModifierMode.Add
                    : ModifierMode.Mul;

                string sourceId =
                    "music_track_" + choiceIndex + "_" + track.TrackId + ":" + modifier.StatKey + ":" + i;
                _stack.AddOrReplace(new RunModifier(sourceId, stat, mode, modifier.Value));
                list.Add(sourceId);
            }
        }

        private void ApplyLegacyFallback(int choiceIndex, int optionIndex)
        {
            float mul = 1f;
            if (optionIndex == 0)
            {
                mul = 1.2f;
            }
            else if (optionIndex == 2)
            {
                mul = 1.4f;
            }

            string sourceId = "music_track_" + choiceIndex + "_legacy:move_speed_mul:0";
            _stack.AddOrReplace(new RunModifier(sourceId, RunStatId.PlayerMoveSpeedMultiplier, ModifierMode.Mul, mul));
            _trackSourceIdsByChoice[choiceIndex].Add(sourceId);
        }

        private void RebuildSynergyModifiers()
        {
            RemoveAllSynergySources();
            if (_library == null)
            {
                return;
            }

            int uniqueCount = BuildGenreCounts();
            for (int i = 0; i < uniqueCount; i++)
            {
                string genreId = _uniqueGenreIds[i];
                int count = _uniqueGenreCounts[i];
                if (string.IsNullOrEmpty(genreId) || count <= 0)
                {
                    continue;
                }

                MusicGenreSO genre;
                if (!_library.TryGetGenre(genreId, out genre) || genre == null)
                {
                    continue;
                }

                MusicSynergySO synergy = _library.GetBestSynergyForGenre(genre, count);
                if (synergy == null || synergy.Modifiers == null)
                {
                    continue;
                }

                for (int m = 0; m < synergy.Modifiers.Length; m++)
                {
                    MusicModifierDef modifier = synergy.Modifiers[m];
                    RunStatId stat;
                    if (!TryMapStatKey(modifier.StatKey, out stat))
                    {
                        continue;
                    }

                    ModifierMode mode = modifier.Mode == MusicModifierMode.Add
                        ? ModifierMode.Add
                        : ModifierMode.Mul;

                    string sourceId = "music_synergy_" + synergy.SynergyId + ":" + modifier.StatKey + ":" + m;
                    _stack.AddOrReplace(new RunModifier(sourceId, stat, mode, modifier.Value));
                    _activeSynergySourceIds.Add(sourceId);
                }
            }
        }

        private int BuildGenreCounts()
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                _uniqueGenreIds[i] = null;
                _uniqueGenreCounts[i] = 0;
            }

            int uniqueCount = 0;
            for (int i = 0; i < ChoiceCount; i++)
            {
                string genreId = _selectedGenreIds[i];
                if (string.IsNullOrEmpty(genreId))
                {
                    continue;
                }

                int found = -1;
                for (int g = 0; g < uniqueCount; g++)
                {
                    if (string.Equals(_uniqueGenreIds[g], genreId, StringComparison.Ordinal))
                    {
                        found = g;
                        break;
                    }
                }

                if (found >= 0)
                {
                    _uniqueGenreCounts[found]++;
                    continue;
                }

                _uniqueGenreIds[uniqueCount] = genreId;
                _uniqueGenreCounts[uniqueCount] = 1;
                uniqueCount++;
            }

            return uniqueCount;
        }
    }
}
