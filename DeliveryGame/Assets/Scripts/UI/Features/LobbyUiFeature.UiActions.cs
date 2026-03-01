using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.UI.Features
{
    internal sealed partial class LobbyUiFeature
    {
        private void OnLobbyStartRunClicked()
        {
            TryPlayLobbyUiClick();
            Events.Publish(new StartRunRequested());
        }

        private void OnLobbySettingsClicked()
        {
            TryPlayLobbyUiClick();
            SetLobbySettingsVisible(_lobbySettingsPanel == null || !_lobbySettingsPanel.activeSelf);
        }

        private void OnLobbyExitClicked()
        {
            TryPlayLobbyUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnLobbyChangeSectorClicked()
        {
            TryPlayLobbyUiClick();
            Events.Publish(new SelectNextRegionRequested());
        }

        private void OnLobbyUnlockRegionClicked()
        {
            if (string.IsNullOrEmpty(_lobbyPendingUnlockRegionId))
            {
                return;
            }

            TryPlayLobbyUiClick();
            Events.Publish(new UnlockRegionRequested
            {
                RegionId = _lobbyPendingUnlockRegionId
            });
        }

        private void OnLobbyBgmSliderChanged(float value)
        {
            if (_lobbyAudioManager == null)
            {
                Services.TryGet(out _lobbyAudioManager);
            }

            if (_lobbyAudioManager != null)
            {
                _lobbyAudioManager.SetBgmVolume01(value);
            }
        }

        private void OnLobbyUiSliderChanged(float value)
        {
            if (_lobbyAudioManager == null)
            {
                Services.TryGet(out _lobbyAudioManager);
            }

            if (_lobbyAudioManager != null)
            {
                _lobbyAudioManager.SetUiVolume01(value);
            }
        }

        private void SetLobbySettingsVisible(bool visible)
        {
            if (_lobbySettingsPanel != null)
            {
                _lobbySettingsPanel.SetActive(visible);
            }
        }

        private void TryPlayLobbyUiClick()
        {
            Ui.PlayUiClick();
        }

        private void DestroyLobbyModuleUi()
        {
            _lobbyBgmSlider = null;
            _lobbyUiSlider = null;
            _lobbySettingsPanel = null;
            _lobbyMetaCashText = null;
            _lobbyMetaRegionText = null;
            _lobbyMetaSelectedRegionText = null;
            _lobbyMetaUpgradeText = null;
            _lobbyMetaUnlockText = null;
            _lobbyUnlockRegionButton = null;
            _lobbyUnlockRegionButtonLabel = null;
            _lobbyPendingUnlockRegionId = null;

            if (_lobbyRoot == null)
            {
                return;
            }

            Object.Destroy(_lobbyRoot);
            _lobbyRoot = null;
        }
    }
}
