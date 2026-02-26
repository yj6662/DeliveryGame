using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunModifierManager : SubManagerBase
    {
        private const int ChoiceCount = 3;
        private const float Epsilon = 0.0001f;

        private readonly string[] _selectedTrackIds = new string[ChoiceCount];
        private readonly string[] _selectedGenreIds = new string[ChoiceCount];
        private readonly string[] _uniqueGenreIds = new string[ChoiceCount];
        private readonly int[] _uniqueGenreCounts = new int[ChoiceCount];

        private readonly List<string>[] _trackSourceIdsByChoice =
        {
            new List<string>(8),
            new List<string>(8),
            new List<string>(8)
        };

        private readonly List<string> _activeSynergySourceIds = new List<string>(16);

        private ModifierStackService _stack;
        private MusicLibraryService _library;
        private float _lastPublishedSpeedMul = 1f;
        private bool _hasRunActive;

        public override string Name => nameof(RunModifierManager);
        public override int InitOrder => 45;

        protected override void OnInitialize()
        {
            _stack = new ModifierStackService();
            Services.Register(_stack);
            Services.TryGet(out _library);

            _lastPublishedSpeedMul = 1f;
            _hasRunActive = false;
            ResetSelectionState();

            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunSessionEnded);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);

            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null && runSessionManager.HasActiveRun)
            {
                _hasRunActive = true;
                ClearAllModifiers("init_active_run", true);
            }
        }

        protected override void OnShutdown()
        {
            ClearAllModifiers("shutdown_clear", true);
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            _hasRunActive = true;
            ClearAllModifiers("run_start_clear", true);
        }

        private void OnRunSessionEnded(DomainRunSessionEnded evt)
        {
            _hasRunActive = false;
            ClearAllModifiers("run_end_clear", true);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (!_hasRunActive)
            {
                return;
            }

            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _hasRunActive = false;
            ClearAllModifiers("scene_transition_clear", true);
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            if (!_hasRunActive)
            {
                return;
            }

            if (evt.ChoiceIndex < 0 || evt.ChoiceIndex >= ChoiceCount)
            {
                return;
            }

            if (_library == null)
            {
                Services.TryGet(out _library);
            }

            int choiceIndex = evt.ChoiceIndex;
            RemoveTrackSources(choiceIndex);
            _selectedTrackIds[choiceIndex] = null;
            _selectedGenreIds[choiceIndex] = null;

            bool applied = false;
            if (_library != null && !string.IsNullOrEmpty(evt.TrackId))
            {
                MusicTrackSO track;
                if (_library.TryGetTrack(evt.TrackId, out track) && track != null)
                {
                    _selectedTrackIds[choiceIndex] = track.TrackId;
                    _selectedGenreIds[choiceIndex] = track.Genre != null ? track.Genre.GenreId : evt.GenreId;
                    ApplyTrackModifiers(choiceIndex, track);
                    applied = true;
                }
            }

            if (!applied)
            {
                ApplyLegacyFallback(choiceIndex, evt.OptionIndex);
            }

            RebuildSynergyModifiers();
            PublishStateChanged("music_choice_" + choiceIndex);
        }

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

        private void RemoveTrackSources(int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= ChoiceCount)
            {
                return;
            }

            List<string> sourceIds = _trackSourceIdsByChoice[choiceIndex];
            for (int i = 0; i < sourceIds.Count; i++)
            {
                _stack.RemoveSource(sourceIds[i]);
            }

            sourceIds.Clear();
        }

        private void RemoveAllSynergySources()
        {
            for (int i = 0; i < _activeSynergySourceIds.Count; i++)
            {
                _stack.RemoveSource(_activeSynergySourceIds[i]);
            }

            _activeSynergySourceIds.Clear();
        }

        private void ClearAllModifiers(string sourceId, bool publishCleared)
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                RemoveTrackSources(i);
                _selectedTrackIds[i] = null;
                _selectedGenreIds[i] = null;
            }

            RemoveAllSynergySources();
            _stack.ClearAll();

            if (publishCleared)
            {
                Events.Publish(new RunModifiersCleared());
            }

            PublishStateChanged(sourceId);
        }

        private void ResetSelectionState()
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                _selectedTrackIds[i] = null;
                _selectedGenreIds[i] = null;
                _trackSourceIdsByChoice[i].Clear();
            }

            _activeSynergySourceIds.Clear();
        }

        private void PublishStateChanged(string sourceId)
        {
            PublishSpeedMulIfChanged(sourceId);
            Events.Publish(new RunModifiersChanged { SourceId = sourceId });
        }

        private void PublishSpeedMulIfChanged(string sourceId)
        {
            float current = _stack.GetMul(RunStatId.PlayerMoveSpeedMultiplier);
            if (Mathf.Abs(current - _lastPublishedSpeedMul) < Epsilon)
            {
                return;
            }

            _lastPublishedSpeedMul = current;
            Events.Publish(new PlayerMoveSpeedMultiplierChanged
            {
                Multiplier = current,
                SourceId = sourceId
            });
        }

        private static bool TryMapStatKey(string statKey, out RunStatId stat)
        {
            if (string.Equals(statKey, "move_speed_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.PlayerMoveSpeedMultiplier;
                return true;
            }

            if (string.Equals(statKey, "bike_grip_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.BikeLateralGripMultiplier;
                return true;
            }

            if (string.Equals(statKey, "bike_brake_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.BikeBrakeForceMultiplier;
                return true;
            }

            if (string.Equals(statKey, "reward_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.RewardMultiplier;
                return true;
            }

            if (string.Equals(statKey, "food_temp_decay_mul", StringComparison.Ordinal) ||
                string.Equals(statKey, "temp_decay_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.FoodTemperatureDecayMultiplier;
                return true;
            }

            if (string.Equals(statKey, "spill_gain_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.FoodSpillGainMultiplier;
                return true;
            }

            if (string.Equals(statKey, "offer_accept_ttl_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.OfferAcceptTtlMultiplier;
                return true;
            }

            if (string.Equals(statKey, "offer_respawn_delay_mul", StringComparison.Ordinal) ||
                string.Equals(statKey, "offer_interval_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.OfferRespawnDelayMultiplier;
                return true;
            }

            stat = default;
            return false;
        }
    }
}
