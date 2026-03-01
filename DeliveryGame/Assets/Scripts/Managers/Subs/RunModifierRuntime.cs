using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;
using DomainRunChoiceConstants = DeliveryRun.Delivery.RunSession.RunChoiceConstants;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class RunModifierRuntime
    {
        private const int ChoiceCount = DomainRunChoiceConstants.ChoiceCount;
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

        private readonly ServiceRegistry _services;
        private readonly EventBus _events;

        private ModifierStackService _stack;
        private MusicLibraryService _library;
        private float _lastPublishedSpeedMul = 1f;
        private bool _hasRunActive;

        internal RunModifierRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal void Initialize()
        {
            _stack = new ModifierStackService();
            _services.Register(_stack);
            _services.TryGet(out _library);

            _lastPublishedSpeedMul = 1f;
            _hasRunActive = false;
            ResetSelectionState();

            RunSessionManager runSessionManager;
            if (_services.TryGet(out runSessionManager) && runSessionManager != null && runSessionManager.HasActiveRun)
            {
                _hasRunActive = true;
                ClearAllModifiers("init_active_run", true);
            }
        }

        internal void Shutdown()
        {
            ClearAllModifiers("shutdown_clear", true);
        }

        internal void OnRunSessionStarted()
        {
            _hasRunActive = true;
            ClearAllModifiers("run_start_clear", true);
        }

        internal void OnRunSessionEnded()
        {
            _hasRunActive = false;
            ClearAllModifiers("run_end_clear", true);
        }

        internal void OnSceneTransitionStarted(SceneTransitionStarted evt)
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

        internal void OnMusicChoiceSelected(MusicChoiceSelected evt)
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
                _services.TryGet(out _library);
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

    }
}
