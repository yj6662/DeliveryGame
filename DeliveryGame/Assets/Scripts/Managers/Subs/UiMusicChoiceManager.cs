using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiMusicChoiceManager : SubManagerBase
    {
        private const int OptionCount = 3;
        private const float ScenePollInterval = 0.25f;

        private readonly string[] _currentTrackIds = new string[OptionCount];

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

            Subs.Add<MusicDraftGenerated>(Events, OnDraftGenerated);
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
            RestoreFallbackTimeScale();
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
                string synergy;
                BuildOptionStrings(i, out title, out sub, out synergy);
                _modal.SetOption(i, title, sub, synergy);
            }
        }

        private void BuildOptionStrings(int optionIndex, out string title, out string sub, out string synergy)
        {
            title = "Unknown Track";
            sub = "Unknown Genre";
            synergy = "Theme: -";

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

            title = string.IsNullOrEmpty(track.DisplayName) ? trackId : track.DisplayName;

            string genreName = "Unknown";
            if (track.Genre != null && !string.IsNullOrEmpty(track.Genre.DisplayName))
            {
                genreName = track.Genre.DisplayName;
            }

            sub = genreName + " ? " + track.Tier;
            synergy = BuildSynergyText(track.Genre);
        }

        private string BuildSynergyText(MusicGenreSO genre)
        {
            if (genre == null)
            {
                return "Theme: -";
            }

            string text = "Theme: " + (string.IsNullOrEmpty(genre.ThemeTitle) ? genre.DisplayName : genre.ThemeTitle);
            if (_musicLibrary == null)
            {
                return text;
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
                    duoName = synergy.DisplayName;
                }
                else if (synergy.RequiredCount == 3)
                {
                    trioName = synergy.DisplayName;
                }
            }

            if (string.IsNullOrEmpty(duoName) && string.IsNullOrEmpty(trioName))
            {
                return text;
            }

            string synergyText = "Synergy: ";
            if (!string.IsNullOrEmpty(duoName))
            {
                synergyText += duoName;
            }

            if (!string.IsNullOrEmpty(duoName) && !string.IsNullOrEmpty(trioName))
            {
                synergyText += " / ";
            }

            if (!string.IsNullOrEmpty(trioName))
            {
                synergyText += trioName;
            }

            return text + "\n" + synergyText;
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
    }
}
