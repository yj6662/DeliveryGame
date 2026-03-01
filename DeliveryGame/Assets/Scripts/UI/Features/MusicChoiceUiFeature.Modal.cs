using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.UI.Features
{
    internal sealed partial class MusicChoiceUiFeature
    {
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

            if (_musicChoiceOptionPresenter == null)
            {
                _musicChoiceOptionPresenter = new MusicChoiceOptionPresenter(_musicChoicePickedGenreByChoice);
            }

            for (int i = 0; i < MusicChoiceOptionCount; i++)
            {
                MusicChoiceOptionViewData optionViewData = _musicChoiceOptionPresenter.BuildOptionViewData(
                    i,
                    MusicChoiceOptionCount,
                    _musicChoiceCurrentTrackIds,
                    _musicChoiceChoiceIndexOpen,
                    _musicChoiceLibrary);
                _musicChoiceModal.SetOption(i, optionViewData.Title, optionViewData.Sub, optionViewData.Detail);
                _musicChoiceModal.SetOptionVisual(i, optionViewData.TierCode, optionViewData.ImmediateSynergy);
            }
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

            if (_musicChoiceLibrary == null)
            {
                Services.TryGet(out _musicChoiceLibrary);
            }

            if (_musicChoiceOptionPresenter == null)
            {
                _musicChoiceOptionPresenter = new MusicChoiceOptionPresenter(_musicChoicePickedGenreByChoice);
            }

            string genreId = _musicChoiceOptionPresenter.ResolveGenreIdForOption(
                optionIndex,
                MusicChoiceOptionCount,
                _musicChoiceCurrentTrackIds,
                _musicChoiceLibrary);

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
