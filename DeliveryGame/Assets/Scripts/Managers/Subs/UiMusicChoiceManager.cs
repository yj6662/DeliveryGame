using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiMusicChoiceManager : SubManagerBase
    {
        private const int OptionCount = 3;
        private const float ScenePollInterval = 0.25f;

        private readonly string[] _currentTrackIds = new string[OptionCount];
        private readonly string[] _pickedGenreByChoice = new string[OptionCount];

        private UiPrefabCatalogSO _catalog;
        private AddressablesService _addressables;
        private AudioManager _audioManager;
        private MusicLibraryService _musicLibrary;

        private GameObject _modalInstance;
        private MusicSelectionModalView _modal;
        private bool _isOpen;
        private bool _modalLoadRequested;
        private int _choiceIndexOpen;
        private float _scenePollElapsed;
        private bool _modalOpenPublished;

        private bool _fallbackPauseCaptured;
        private float _fallbackSavedTimeScale;

        public override string Name => nameof(UiMusicChoiceManager);
        public override int InitOrder => 35;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            Services.TryGet(out _addressables);
            Services.TryGet(out _audioManager);
            Services.TryGet(out _musicLibrary);

            _choiceIndexOpen = -1;
            ClearDraftCache();
            ClearPickedGenres();

            Subs.Add<MusicDraftGenerated>(Events, OnDraftGenerated);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (!_isOpen)
            {
                return;
            }

            _scenePollElapsed += unscaledDeltaTime;
            if (_scenePollElapsed < ScenePollInterval)
            {
                return;
            }

            _scenePollElapsed = 0f;
            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                HideModalAndResume();
            }
        }

        protected override void OnShutdown()
        {
            HideModalAndResume();
            PublishModalState(false);
            RestoreFallbackTimeScale();
            ClearPickedGenres();
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            ClearPickedGenres();
            _choiceIndexOpen = -1;
            ClearDraftCache();
        }

        private void OnDraftGenerated(MusicDraftGenerated evt)
        {
            if (_isOpen)
            {
                Debug.LogWarning("[UiMusicChoiceManager] Received draft while modal is already open. Ignored.");
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                return;
            }

            _choiceIndexOpen = evt.ChoiceIndex;
            _currentTrackIds[0] = evt.TrackId0;
            _currentTrackIds[1] = evt.TrackId1;
            _currentTrackIds[2] = evt.TrackId2;

            ShowModal();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To != SceneNames.RunScene)
            {
                HideModalAndResume();
            }
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName != SceneNames.RunScene)
            {
                HideModalAndResume();
            }
        }

        private void ShowModal()
        {
            UiEventSystemBootstrap.EnsureNow();

            if (_catalog == null)
            {
                _catalog = UiPrefabCatalogLoader.LoadOrNull();
                if (_catalog == null)
                {
                    return;
                }
            }

            if (_modalLoadRequested)
            {
                return;
            }

            if (_addressables == null)
            {
                Services.TryGet(out _addressables);
            }

            PauseGameplayOnly();
            _isOpen = true;
            _scenePollElapsed = 0f;
            PublishModalState(true);

            if (_addressables != null && _addressables.IsAvailable && !string.IsNullOrEmpty(_catalog.MusicSelectionModalKey))
            {
                _modalLoadRequested = true;
                _addressables.InstantiatePrefab(_catalog.MusicSelectionModalKey, null, OnModalInstantiated);
                return;
            }

            if (_catalog.MusicSelectionModalPrefab == null)
            {
                Debug.LogError("[UiMusicChoiceManager] MusicSelectionModal key/prefab is not available.");
                HideModalAndResume();
                return;
            }

            GameObject modalObject = Object.Instantiate(_catalog.MusicSelectionModalPrefab);
            BindModalInstance(modalObject);
        }

        private void OnModalInstantiated(GameObject modalObject)
        {
            _modalLoadRequested = false;
            if (modalObject == null)
            {
                HideModalAndResume();
                return;
            }

            if (!_isOpen || SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                ReleaseModalObject(modalObject);
                return;
            }

            BindModalInstance(modalObject);
        }

        private void BindModalInstance(GameObject modalObject)
        {
            _modalInstance = modalObject;
            _modal = modalObject != null ? modalObject.GetComponent<MusicSelectionModalView>() : null;
            if (_modal == null)
            {
                Debug.LogError("[UiMusicChoiceManager] MusicSelectionModal prefab missing MusicSelectionModalView.");
                ReleaseModalObject(modalObject);
                HideModalAndResume();
                return;
            }

            ConfigureModalOptions();
            _modal.Show(OnSelected);
        }

        private void ConfigureModalOptions()
        {
            if (_modal == null)
            {
                return;
            }

            if (_musicLibrary == null)
            {
                Services.TryGet(out _musicLibrary);
            }

            for (int i = 0; i < OptionCount; i++)
            {
                string title;
                string sub;
                string detail;
                int tierCode;
                bool immediateSynergy;
                BuildOptionStrings(i, out title, out sub, out detail, out tierCode, out immediateSynergy);
                _modal.SetOption(i, title, sub, detail);
                _modal.SetOptionVisual(i, tierCode, immediateSynergy);
            }
        }

        private void BuildOptionStrings(
            int optionIndex,
            out string title,
            out string sub,
            out string detail,
            out int tierCode,
            out bool immediateSynergy)
        {
            title = "Unknown Track";
            sub = "Unknown Genre";
            detail = "Theme: -";
            tierCode = 0;
            immediateSynergy = false;

            if (optionIndex < 0 || optionIndex >= OptionCount)
            {
                return;
            }

            string trackId = _currentTrackIds[optionIndex];
            if (string.IsNullOrEmpty(trackId) || _musicLibrary == null)
            {
                return;
            }

            MusicTrackSO track;
            if (!_musicLibrary.TryGetTrack(trackId, out track) || track == null)
            {
                return;
            }

            title = NormalizeTrackCardTitle(string.IsNullOrEmpty(track.DisplayName) ? trackId : track.DisplayName);
            tierCode = ToTierCode(track.Tier);

            string genreName = "Unknown";
            if (track.Genre != null && !string.IsNullOrEmpty(track.Genre.DisplayName))
            {
                genreName = track.Genre.DisplayName;
            }

            sub = genreName + " | " + track.Tier.ToString().ToUpperInvariant();
            detail = BuildDetailText(track);
            immediateSynergy = WouldActivateSynergyNow(track.Genre);
        }

        private static int ToTierCode(MusicTier tier)
        {
            if (tier == MusicTier.Rare) return 1;
            if (tier == MusicTier.Epic) return 2;
            return 0;
        }

        private string BuildDetailText(MusicTrackSO track)
        {
            if (track == null)
            {
                return "Theme: -";
            }

            string themeTitle = "Theme: -";
            string synergyLine = "Synergy: -";
            if (track.Genre != null)
            {
                string theme = string.IsNullOrEmpty(track.Genre.ThemeTitle) ? track.Genre.DisplayName : track.Genre.ThemeTitle;
                themeTitle = "Theme: " + theme;
                synergyLine = BuildSynergyLine(track.Genre);
            }

            string buffLine = BuildModifierSummary(track.Modifiers);
            return themeTitle + "\n" + synergyLine + "\n" + buffLine;
        }

        private string BuildSynergyLine(MusicGenreSO genre)
        {
            if (genre == null || _musicLibrary == null)
            {
                return "Synergy: -";
            }

            string duoName = null;
            string trioName = null;
            foreach (MusicSynergySO synergy in _musicLibrary.GetSynergiesForGenre(genre))
            {
                if (synergy == null)
                {
                    continue;
                }

                if (synergy.RequiredCount == 2)
                {
                    duoName = string.IsNullOrEmpty(synergy.DisplayName) ? "Duo" : synergy.DisplayName;
                }
                else if (synergy.RequiredCount == 3)
                {
                    trioName = string.IsNullOrEmpty(synergy.DisplayName) ? "Trio" : synergy.DisplayName;
                }
            }

            if (string.IsNullOrEmpty(duoName) && string.IsNullOrEmpty(trioName))
            {
                return "Synergy: -";
            }

            if (!string.IsNullOrEmpty(duoName) && !string.IsNullOrEmpty(trioName))
            {
                return "Synergy: " + duoName + " / " + trioName;
            }

            return "Synergy: " + (!string.IsNullOrEmpty(duoName) ? duoName : trioName);
        }

        private bool WouldActivateSynergyNow(MusicGenreSO genre)
        {
            if (genre == null || _musicLibrary == null || string.IsNullOrEmpty(genre.GenreId))
            {
                return false;
            }

            int existingCount = CountPickedGenre(genre.GenreId, _choiceIndexOpen);
            MusicSynergySO before = _musicLibrary.GetBestSynergyForGenre(genre, existingCount);
            MusicSynergySO after = _musicLibrary.GetBestSynergyForGenre(genre, existingCount + 1);
            if (after == null)
            {
                return false;
            }

            if (before == null)
            {
                return true;
            }

            if (after.RequiredCount > before.RequiredCount)
            {
                return true;
            }

            return !string.Equals(after.SynergyId, before.SynergyId, System.StringComparison.Ordinal);
        }

        private int CountPickedGenre(string genreId, int skipChoiceIndex)
        {
            if (string.IsNullOrEmpty(genreId))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _pickedGenreByChoice.Length; i++)
            {
                if (i == skipChoiceIndex)
                {
                    continue;
                }

                if (string.Equals(_pickedGenreByChoice[i], genreId, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string NormalizeTrackCardTitle(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            string title = raw.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (title.Length <= 0)
            {
                return string.Empty;
            }

            int end = title.Length - 1;
            while (end >= 0 && char.IsDigit(title[end]))
            {
                end--;
            }

            if (end < title.Length - 1)
            {
                while (end >= 0 && char.IsWhiteSpace(title[end]))
                {
                    end--;
                }

                if (end >= 0)
                {
                    title = title.Substring(0, end + 1).TrimEnd();
                }
            }

            return title;
        }

        private static string BuildModifierSummary(MusicModifierDef[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return "BUFF: -";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(96);
            builder.Append("BUFF: ");
            int appended = 0;
            for (int i = 0; i < modifiers.Length; i++)
            {
                MusicModifierDef modifier = modifiers[i];
                string label = ToShortStatLabel(modifier.StatKey);
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }

                if (appended > 0)
                {
                    builder.Append(", ");
                }

                if (modifier.Mode == MusicModifierMode.Mul)
                {
                    float pct = (modifier.Value - 1f) * 100f;
                    builder.Append(label).Append(' ').Append(pct.ToString("+0;-0")).Append('%');
                }
                else
                {
                    builder.Append(label).Append(' ').Append(modifier.Value.ToString("+0.##;-0.##"));
                }
                appended++;
            }

            if (appended <= 0)
            {
                return "BUFF: -";
            }

            return builder.ToString();
        }

        private static string ToShortStatLabel(string statKey)
        {
            if (string.IsNullOrEmpty(statKey))
            {
                return null;
            }

            if (string.Equals(statKey, "move_speed_mul", System.StringComparison.Ordinal)) return "SPD";
            if (string.Equals(statKey, "bike_grip_mul", System.StringComparison.Ordinal)) return "GRIP";
            if (string.Equals(statKey, "bike_brake_mul", System.StringComparison.Ordinal)) return "BRAKE";
            if (string.Equals(statKey, "reward_mul", System.StringComparison.Ordinal)) return "REWARD";
            if (string.Equals(statKey, "food_temp_decay_mul", System.StringComparison.Ordinal) ||
                string.Equals(statKey, "temp_decay_mul", System.StringComparison.Ordinal)) return "TEMP";
            if (string.Equals(statKey, "spill_gain_mul", System.StringComparison.Ordinal)) return "SPILL";
            if (string.Equals(statKey, "offer_accept_ttl_mul", System.StringComparison.Ordinal)) return "TTL";
            if (string.Equals(statKey, "offer_respawn_delay_mul", System.StringComparison.Ordinal) ||
                string.Equals(statKey, "offer_interval_mul", System.StringComparison.Ordinal)) return "RESPAWN";
            return statKey;
        }

        private void OnSelected(int optionIndex)
        {
            if (!_isOpen)
            {
                return;
            }

            if (optionIndex < 0 || optionIndex >= OptionCount)
            {
                optionIndex = 0;
            }

            string trackId = _currentTrackIds[optionIndex];
            string genreId = string.Empty;

            if (_musicLibrary == null)
            {
                Services.TryGet(out _musicLibrary);
            }

            if (_musicLibrary != null && !string.IsNullOrEmpty(trackId))
            {
                MusicTrackSO track;
                if (_musicLibrary.TryGetTrack(trackId, out track) && track != null && track.Genre != null)
                {
                    genreId = track.Genre.GenreId;
                }
            }

            if (_choiceIndexOpen >= 0 && _choiceIndexOpen < _pickedGenreByChoice.Length)
            {
                _pickedGenreByChoice[_choiceIndexOpen] = genreId;
            }

            TryPlayUiClick();
            Events.Publish(new MusicChoiceSelected
            {
                ChoiceIndex = _choiceIndexOpen,
                OptionIndex = optionIndex,
                TrackId = trackId,
                GenreId = genreId
            });

            HideModal();
            ResumeGameplayOnly();
            PublishModalState(false);
            _isOpen = false;
            _choiceIndexOpen = -1;
            ClearDraftCache();
        }

        private void HideModalAndResume()
        {
            if (!_isOpen && _modal == null)
            {
                return;
            }

            HideModal();
            ResumeGameplayOnly();
            PublishModalState(false);
            _isOpen = false;
            _choiceIndexOpen = -1;
            ClearDraftCache();
        }

        private void HideModal()
        {
            if (_modal != null)
            {
                _modal.Hide();
            }

            if (_modalInstance != null)
            {
                ReleaseModalObject(_modalInstance);
            }

            _modal = null;
            _modalInstance = null;
            _modalLoadRequested = false;
        }

        private void PauseGameplayOnly()
        {
            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null)
            {
                runSessionManager.EnterPauseForChoice();
                return;
            }

            CaptureFallbackTimeScale();
            Time.timeScale = 0f;
        }

        private void ResumeGameplayOnly()
        {
            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null)
            {
                runSessionManager.ResumeFromChoice();
                RestoreFallbackTimeScale();
                return;
            }

            RestoreFallbackTimeScale();
        }

        private void CaptureFallbackTimeScale()
        {
            if (_fallbackPauseCaptured)
            {
                return;
            }

            _fallbackSavedTimeScale = Time.timeScale;
            _fallbackPauseCaptured = true;
        }

        private void RestoreFallbackTimeScale()
        {
            if (!_fallbackPauseCaptured)
            {
                return;
            }

            Time.timeScale = _fallbackSavedTimeScale;
            _fallbackSavedTimeScale = 0f;
            _fallbackPauseCaptured = false;
        }

        private void TryPlayUiClick()
        {
            if (_audioManager == null)
            {
                Services.TryGet(out _audioManager);
            }

            if (_audioManager == null || _catalog == null)
            {
                return;
            }

            _audioManager.PlayUiClick(_catalog.UiClickKey);
        }

        private void ReleaseModalObject(GameObject modalObject)
        {
            if (modalObject == null)
            {
                return;
            }

            if (_addressables == null)
            {
                Services.TryGet(out _addressables);
            }

            if (_addressables != null)
            {
                _addressables.ReleaseInstance(modalObject);
                return;
            }

            Object.Destroy(modalObject);
        }

        private void ClearDraftCache()
        {
            for (int i = 0; i < OptionCount; i++)
            {
                _currentTrackIds[i] = null;
            }
        }

        private void ClearPickedGenres()
        {
            for (int i = 0; i < _pickedGenreByChoice.Length; i++)
            {
                _pickedGenreByChoice[i] = null;
            }
        }

        private void PublishModalState(bool isOpen)
        {
            if (isOpen == _modalOpenPublished)
            {
                return;
            }

            _modalOpenPublished = isOpen;
            Events.Publish(new MusicChoiceModalStateChanged
            {
                IsOpen = isOpen
            });
        }
    }
}
