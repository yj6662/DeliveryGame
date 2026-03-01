using DeliveryRun;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Lobby;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class LobbyHubRuntime
    {
        private const float ScenePollInterval = 0.25f;
        private const float ScanInterval = 0.1f;

        private static readonly string[] UpgradeIds = MetaProgressionConstants.UpgradeIds;

        private readonly ServiceRegistry _services;
        private readonly EventBus _events;

        private LobbyAvatarController _player;
        private readonly LobbyInteractZone[] _zones = new LobbyInteractZone[24];
        private int _zoneCount;
        private LobbyInteractZone _activeZone;

        private MetaProgressionService _meta;
        private UiPrefabCatalogSO _catalog;
        private AudioManager _audio;

        private bool _isLobby;
        private float _scenePollElapsed;
        private float _scanElapsed;

        private GameObject _uiRoot;
        private Text _promptText;
        private Text _metaText;
        private GameObject _garagePanel;
        private readonly Text[] _upgradeRows = new Text[7];
        private readonly Button[] _upgradeButtons = new Button[7];
        private GameObject _regionPanel;
        private Text _regionText;
        private Button _unlockButton;
        private Text _unlockButtonText;
        private string _pendingUnlockRegionId;
        private Sprite _panelSkinSprite;
        private Sprite _buttonSkinSprite;
        private Sprite _buttonAccentSkinSprite;

        internal LobbyHubRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal void Initialize()
        {
            _services.TryGet(out _meta);
            _services.TryGet(out _audio);
            _catalog = UiPrefabCatalogLoader.LoadOrNull();

            HandleScene(SceneManager.GetActiveScene().name);
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(ref _scenePollElapsed, ScenePollInterval, unscaledDeltaTime))
            {
                HandleScene(SceneManager.GetActiveScene().name);
            }

            if (!_isLobby)
            {
                return;
            }

            EnsureRefs();
            _scanElapsed += unscaledDeltaTime;
            if (_scanElapsed >= ScanInterval)
            {
                _scanElapsed = 0f;
                ScanNearestZone();
                RefreshPrompt();
            }

            if (_activeZone != null && RuntimeInput.ConsumeInteractPressedThisFrame())
            {
                Interact(_activeZone);
            }
        }

        internal void Shutdown()
        {
            _isLobby = false;
            DestroyUi();
        }

        internal void OnTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _isLobby = false;
            DestroyUi();
        }

        internal void OnTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleScene(evt.SceneName);
        }

        internal void OnMetaDirty()
        {
            RefreshMeta();
        }

        private void HandleScene(string sceneName)
        {
            bool shouldLobby = sceneName == SceneNames.LobbyScene;
            if (_isLobby == shouldLobby)
            {
                if (_isLobby)
                {
                    EnsureRefs();
                    EnsureUi();
                }

                return;
            }

            _isLobby = shouldLobby;
            _zoneCount = 0;
            _activeZone = null;
            if (_isLobby)
            {
                EnsureRefs();
                EnsureUi();
                return;
            }

            DestroyUi();
        }

        private void EnsureRefs()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<LobbyAvatarController>();
            }

            if (_meta == null)
            {
                _services.TryGet(out _meta);
            }

            if (_audio == null)
            {
                _services.TryGet(out _audio);
            }

            if (_zoneCount <= 0)
            {
                LobbyInteractZone[] zones = Object.FindObjectsByType<LobbyInteractZone>(FindObjectsSortMode.None);
                _zoneCount = zones.Length < _zones.Length ? zones.Length : _zones.Length;
                for (int i = 0; i < _zoneCount; i++)
                {
                    _zones[i] = zones[i];
                }
            }
        }

        private void ScanNearestZone()
        {
            _activeZone = null;
            if (_player == null)
            {
                return;
            }

            float best = float.MaxValue;
            Vector3 p = _player.transform.position;
            for (int i = 0; i < _zoneCount; i++)
            {
                LobbyInteractZone zone = _zones[i];
                if (zone == null || !zone.gameObject.activeInHierarchy || !zone.IsInRange(p))
                {
                    continue;
                }

                Vector3 d = zone.transform.position - p;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr >= best)
                {
                    continue;
                }

                best = sqr;
                _activeZone = zone;
            }
        }

        private void Interact(LobbyInteractZone zone)
        {
            if (zone == null)
            {
                return;
            }

            PlayUiClick();
            switch (zone.InteractType)
            {
                case LobbyInteractType.StartDelivery:
                    _events.Publish(new StartRunRequested());
                    break;
                case LobbyInteractType.OpenGaragePanel:
                    TogglePanel(_garagePanel);
                    break;
                case LobbyInteractType.OpenRegionPanel:
                    TogglePanel(_regionPanel);
                    break;
                case LobbyInteractType.OpenSettingsPanel:
                    break;
                case LobbyInteractType.ExitGame:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        private void OnUpgradeClicked(int index)
        {
            if (index < 0 || index >= UpgradeIds.Length)
            {
                return;
            }

            PlayUiClick();
            _events.Publish(new PermanentUpgradePurchaseRequested { UpgradeId = UpgradeIds[index] });
        }

        private void OnUnlockClicked()
        {
            if (string.IsNullOrEmpty(_pendingUnlockRegionId))
            {
                return;
            }

            PlayUiClick();
            _events.Publish(new UnlockRegionRequested { RegionId = _pendingUnlockRegionId });
        }

        private void RefreshPrompt()
        {
            if (_promptText == null)
            {
                return;
            }

            _promptText.text = _activeZone == null ? string.Empty : (_activeZone.PromptText + " [F]");
        }

        private void TogglePanel(GameObject panel)
        {
            TogglePanel(panel, panel != null && !panel.activeSelf);
        }

        private void TogglePanel(GameObject panel, bool active)
        {
            if (_garagePanel != null && _garagePanel != panel) _garagePanel.SetActive(false);
            if (_regionPanel != null && _regionPanel != panel) _regionPanel.SetActive(false);
            if (panel != null) panel.SetActive(active);
            RefreshMeta();
        }

        private void PlayUiClick()
        {
            if (_audio == null || _catalog == null)
            {
                return;
            }

            _audio.PlayUiClick(_catalog.UiClickKey);
        }
    }
}
