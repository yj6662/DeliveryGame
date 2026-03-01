using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;


namespace DeliveryRun.UI.Features
{
    internal sealed class MusicChoiceUiFeature : UiFeatureBase
    {
        protected override void OnInitialize()
        {
            InitializeMusicChoiceModule();
        }
        protected override void OnTick(float unscaledDeltaTime)
        {
            TickMusicChoiceModule(unscaledDeltaTime);
        }
        protected override void OnShutdown()
        {
            ShutdownMusicChoiceModule();
        }

        private const int MusicChoiceOptionCount = 3;
        private const float MusicChoiceScenePollInterval = 0.25f;

        private readonly string[] _musicChoiceCurrentTrackIds = new string[MusicChoiceOptionCount];
        private readonly string[] _musicChoicePickedGenreByChoice = new string[MusicChoiceOptionCount];

        private MusicLibraryService _musicChoiceLibrary;

        private GameObject _musicChoiceModalInstance;
        private MusicSelectionModalView _musicChoiceModal;
        private bool _musicChoiceIsOpen;
        private bool _musicChoiceModalLoadRequested;
        private int _musicChoiceChoiceIndexOpen;
        private float _musicChoiceScenePollElapsed;
        private bool _musicChoiceModalOpenPublished;

        private bool _musicChoiceFallbackPauseCaptured;
        private float _musicChoiceFallbackSavedTimeScale;

        private void InitializeMusicChoiceModule()
        {
            Services.TryGet(out _musicChoiceLibrary);

            _musicChoiceChoiceIndexOpen = -1;
            ClearMusicChoiceDraftCache();
            ClearMusicChoicePickedGenres();

            Subs.Add<MusicDraftGenerated>(Events, OnMusicChoiceDraftGenerated);
            Subs.Add<MusicChoiceAutoResolved>(Events, OnMusicChoiceAutoResolved);
            Subs.Add<DomainRunSessionStarted>(Events, OnMusicChoiceRunSessionStarted);
            Subs.Add<SceneTransitionStarted>(Events, OnMusicChoiceSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnMusicChoiceSceneTransitionCompleted);
        }

        private void TickMusicChoiceModule(float unscaledDeltaTime)
        {
            if (!_musicChoiceIsOpen)
            {
                return;
            }

            _musicChoiceScenePollElapsed += unscaledDeltaTime;
            if (_musicChoiceScenePollElapsed < MusicChoiceScenePollInterval)
            {
                return;
            }

            _musicChoiceScenePollElapsed = 0f;
            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                HideMusicChoiceModalAndResume();
            }
        }

        private void ShutdownMusicChoiceModule()
        {
            HideMusicChoiceModalAndResume();
            PublishMusicChoiceModalState(false);
            RestoreMusicChoiceFallbackTimeScale();
            ClearMusicChoicePickedGenres();
        }

        private void OnMusicChoiceRunSessionStarted(DomainRunSessionStarted evt)
        {
            ClearMusicChoicePickedGenres();
            _musicChoiceChoiceIndexOpen = -1;
            ClearMusicChoiceDraftCache();
        }

        private void OnMusicChoiceDraftGenerated(MusicDraftGenerated evt)
        {
            if (_musicChoiceIsOpen)
            {
                Debug.LogWarning("[UiMusicChoiceManager] Received draft while modal is already open. Ignored.");
                return;
            }

            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                return;
            }

            _musicChoiceChoiceIndexOpen = evt.ChoiceIndex;
            _musicChoiceCurrentTrackIds[0] = evt.TrackId0;
            _musicChoiceCurrentTrackIds[1] = evt.TrackId1;
            _musicChoiceCurrentTrackIds[2] = evt.TrackId2;

            ShowMusicChoiceModal();
        }

        private void OnMusicChoiceAutoResolved(MusicChoiceAutoResolved evt)
        {
            if (!_musicChoiceIsOpen || evt.ChoiceIndex != _musicChoiceChoiceIndexOpen)
            {
                return;
            }

            if (_musicChoiceChoiceIndexOpen >= 0 && _musicChoiceChoiceIndexOpen < _musicChoicePickedGenreByChoice.Length)
            {
                _musicChoicePickedGenreByChoice[_musicChoiceChoiceIndexOpen] = evt.GenreId;
            }

            HideMusicChoiceModalAndResume();
        }

        private void OnMusicChoiceSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To != SceneNames.RunScene)
            {
                HideMusicChoiceModalAndResume();
            }
        }

        private void OnMusicChoiceSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName != SceneNames.RunScene)
            {
                HideMusicChoiceModalAndResume();
            }
        }

        private void ShowMusicChoiceModal()
        {
            Ui.EnsureEventSystem();

            if (_musicChoiceModalLoadRequested)
            {
                return;
            }

            PauseMusicChoiceGameplayOnly();
            _musicChoiceIsOpen = true;
            _musicChoiceScenePollElapsed = 0f;
            PublishMusicChoiceModalState(true);
            _musicChoiceModalLoadRequested = true;
            Ui.InstantiateMusicSelectionModal(OnMusicChoiceModalInstantiated);
        }

        private void OnMusicChoiceModalInstantiated(GameObject modalObject)
        {
            _musicChoiceModalLoadRequested = false;
            if (modalObject == null)
            {
                HideMusicChoiceModalAndResume();
                return;
            }

            if (!_musicChoiceIsOpen || SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                ReleaseMusicChoiceModalObject(modalObject);
                return;
            }

            BindMusicChoiceModalInstance(modalObject);
        }

        private void BindMusicChoiceModalInstance(GameObject modalObject)
        {
            _musicChoiceModalInstance = modalObject;
            _musicChoiceModal = modalObject != null ? modalObject.GetComponent<MusicSelectionModalView>() : null;
            if (_musicChoiceModal == null)
            {
                Debug.LogError("[UiMusicChoiceManager] MusicSelectionModal prefab missing MusicSelectionModalView.");
                ReleaseMusicChoiceModalObject(modalObject);
                HideMusicChoiceModalAndResume();
                return;
            }

            ConfigureMusicChoiceModalOptions();
            _musicChoiceModal.Show(OnMusicChoiceSelectedOption);
        }

        private void ConfigureMusicChoiceModalOptions()
        {
            if (_musicChoiceModal == null)
            {
                return;
            }

            if (_musicChoiceLibrary == null)
            {
                Services.TryGet(out _musicChoiceLibrary);
            }

            for (int i = 0; i < MusicChoiceOptionCount; i++)
            {
                string title;
                string sub;
                string detail;
                int tierCode;
                bool immediateSynergy;
                BuildMusicChoiceOptionStrings(i, out title, out sub, out detail, out tierCode, out immediateSynergy);
                _musicChoiceModal.SetOption(i, title, sub, detail);
                _musicChoiceModal.SetOptionVisual(i, tierCode, immediateSynergy);
            }
        }

        private void BuildMusicChoiceOptionStrings(
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

            if (optionIndex < 0 || optionIndex >= MusicChoiceOptionCount)
            {
                return;
            }

            string trackId = _musicChoiceCurrentTrackIds[optionIndex];
            if (string.IsNullOrEmpty(trackId) || _musicChoiceLibrary == null)
            {
                return;
            }

            MusicTrackSO track;
            if (!_musicChoiceLibrary.TryGetTrack(trackId, out track) || track == null)
            {
                return;
            }

            title = NormalizeMusicChoiceTrackCardTitle(string.IsNullOrEmpty(track.DisplayName) ? trackId : track.DisplayName);
            tierCode = ToMusicChoiceTierCode(track.Tier);

            string genreName = "Unknown";
            if (track.Genre != null && !string.IsNullOrEmpty(track.Genre.DisplayName))
            {
                genreName = track.Genre.DisplayName;
            }

            sub = genreName + " | " + track.Tier.ToString().ToUpperInvariant();
            detail = BuildMusicChoiceDetailText(track);
            immediateSynergy = WouldMusicChoiceActivateSynergyNow(track.Genre);
        }

        private static int ToMusicChoiceTierCode(MusicTier tier)
        {
            if (tier == MusicTier.Rare) return 1;
            if (tier == MusicTier.Epic) return 2;
            return 0;
        }

        private string BuildMusicChoiceDetailText(MusicTrackSO track)
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
                synergyLine = BuildMusicChoiceSynergyLine(track.Genre);
            }

            string buffLine = BuildMusicChoiceModifierSummary(track.Modifiers);
            return themeTitle + "\n" + synergyLine + "\n" + buffLine;
        }

        private string BuildMusicChoiceSynergyLine(MusicGenreSO genre)
        {
            if (genre == null || _musicChoiceLibrary == null)
            {
                return "Synergy: -";
            }

            string duoName = null;
            string trioName = null;
            foreach (MusicSynergySO synergy in _musicChoiceLibrary.GetSynergiesForGenre(genre))
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

        private bool WouldMusicChoiceActivateSynergyNow(MusicGenreSO genre)
        {
            if (genre == null || _musicChoiceLibrary == null || string.IsNullOrEmpty(genre.GenreId))
            {
                return false;
            }

            int existingCount = CountMusicChoicePickedGenre(genre.GenreId, _musicChoiceChoiceIndexOpen);
            MusicSynergySO before = _musicChoiceLibrary.GetBestSynergyForGenre(genre, existingCount);
            MusicSynergySO after = _musicChoiceLibrary.GetBestSynergyForGenre(genre, existingCount + 1);
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

        private int CountMusicChoicePickedGenre(string genreId, int skipChoiceIndex)
        {
            if (string.IsNullOrEmpty(genreId))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _musicChoicePickedGenreByChoice.Length; i++)
            {
                if (i == skipChoiceIndex)
                {
                    continue;
                }

                if (string.Equals(_musicChoicePickedGenreByChoice[i], genreId, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string NormalizeMusicChoiceTrackCardTitle(string raw)
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

        private static string BuildMusicChoiceModifierSummary(MusicModifierDef[] modifiers)
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
                string label = ToMusicChoiceShortStatLabel(modifier.StatKey);
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

        private static string ToMusicChoiceShortStatLabel(string statKey)
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

        private void OnMusicChoiceSelectedOption(int optionIndex)
        {
            if (!_musicChoiceIsOpen)
            {
                return;
            }

            if (optionIndex < 0 || optionIndex >= MusicChoiceOptionCount)
            {
                optionIndex = 0;
            }

            string trackId = _musicChoiceCurrentTrackIds[optionIndex];
            string genreId = string.Empty;

            if (_musicChoiceLibrary == null)
            {
                Services.TryGet(out _musicChoiceLibrary);
            }

            if (_musicChoiceLibrary != null && !string.IsNullOrEmpty(trackId))
            {
                MusicTrackSO track;
                if (_musicChoiceLibrary.TryGetTrack(trackId, out track) && track != null && track.Genre != null)
                {
                    genreId = track.Genre.GenreId;
                }
            }

            if (_musicChoiceChoiceIndexOpen >= 0 && _musicChoiceChoiceIndexOpen < _musicChoicePickedGenreByChoice.Length)
            {
                _musicChoicePickedGenreByChoice[_musicChoiceChoiceIndexOpen] = genreId;
            }

            TryPlayMusicChoiceUiClick();
            Events.Publish(new MusicChoiceSelected
            {
                ChoiceIndex = _musicChoiceChoiceIndexOpen,
                OptionIndex = optionIndex,
                TrackId = trackId,
                GenreId = genreId
            });

            HideMusicChoiceModal();
            ResumeMusicChoiceGameplayOnly();
            PublishMusicChoiceModalState(false);
            _musicChoiceIsOpen = false;
            _musicChoiceChoiceIndexOpen = -1;
            ClearMusicChoiceDraftCache();
        }

        private void HideMusicChoiceModalAndResume()
        {
            if (!_musicChoiceIsOpen && _musicChoiceModal == null)
            {
                return;
            }

            HideMusicChoiceModal();
            ResumeMusicChoiceGameplayOnly();
            PublishMusicChoiceModalState(false);
            _musicChoiceIsOpen = false;
            _musicChoiceChoiceIndexOpen = -1;
            ClearMusicChoiceDraftCache();
        }

        private void HideMusicChoiceModal()
        {
            if (_musicChoiceModal != null)
            {
                _musicChoiceModal.Hide();
            }

            if (_musicChoiceModalInstance != null)
            {
                ReleaseMusicChoiceModalObject(_musicChoiceModalInstance);
            }

            _musicChoiceModal = null;
            _musicChoiceModalInstance = null;
            _musicChoiceModalLoadRequested = false;
        }

        private void PauseMusicChoiceGameplayOnly()
        {
            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null)
            {
                runSessionManager.EnterPauseForChoice();
                return;
            }

            CaptureMusicChoiceFallbackTimeScale();
            Time.timeScale = 0f;
        }

        private void ResumeMusicChoiceGameplayOnly()
        {
            RunSessionManager runSessionManager;
            if (Services.TryGet(out runSessionManager) && runSessionManager != null)
            {
                runSessionManager.ResumeFromChoice();
                RestoreMusicChoiceFallbackTimeScale();
                return;
            }

            RestoreMusicChoiceFallbackTimeScale();
        }

        private void CaptureMusicChoiceFallbackTimeScale()
        {
            if (_musicChoiceFallbackPauseCaptured)
            {
                return;
            }

            _musicChoiceFallbackSavedTimeScale = Time.timeScale;
            _musicChoiceFallbackPauseCaptured = true;
        }

        private void RestoreMusicChoiceFallbackTimeScale()
        {
            if (!_musicChoiceFallbackPauseCaptured)
            {
                return;
            }

            Time.timeScale = _musicChoiceFallbackSavedTimeScale;
            _musicChoiceFallbackSavedTimeScale = 0f;
            _musicChoiceFallbackPauseCaptured = false;
        }

        private void TryPlayMusicChoiceUiClick()
        {
            Ui.PlayUiClick();
        }

        private void ReleaseMusicChoiceModalObject(GameObject modalObject)
        {
            if (modalObject == null)
            {
                return;
            }

            Ui.ReleaseUiInstance(modalObject);
        }

        private void ClearMusicChoiceDraftCache()
        {
            for (int i = 0; i < MusicChoiceOptionCount; i++)
            {
                _musicChoiceCurrentTrackIds[i] = null;
            }
        }

        private void ClearMusicChoicePickedGenres()
        {
            for (int i = 0; i < _musicChoicePickedGenreByChoice.Length; i++)
            {
                _musicChoicePickedGenreByChoice[i] = null;
            }
        }

        private void PublishMusicChoiceModalState(bool isOpen)
        {
            if (isOpen == _musicChoiceModalOpenPublished)
            {
                return;
            }

            _musicChoiceModalOpenPublished = isOpen;
            Events.Publish(new MusicChoiceModalStateChanged
            {
                IsOpen = isOpen
            });
        }
    }
}
