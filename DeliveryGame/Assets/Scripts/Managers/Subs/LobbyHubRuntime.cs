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
        private enum LobbyViewMode
        {
            Home,
            Garage,
            Region
        }

        private const float ScenePollInterval = 0.25f;
        private const float ScanInterval = 0.1f;
        private const string SceneUiRootName = "LobbySceneUiRoot";
        private const string SceneUiPanelName = "MainPanel";
        private const string StartButtonName = "StartRunButton";
        private const string ExitButtonName = "ExitButton";
        private const string StatusTextName = "StatusText";

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
        private bool _sceneUiOnlyMode;
        private float _scenePollElapsed;
        private float _scanElapsed;
        private LobbyViewMode _currentView = LobbyViewMode.Home;
        private GameObject _sceneUiRoot;
        private GameObject _sceneUiPanel;
        private Button _sceneUiStartButton;
        private Button _sceneUiExitButton;
        private Text _sceneUiStatusText;

        private GameObject _uiRoot;
        private Text _promptText;
        private Text _metaText;
        private GameObject _mainPanel;
        private Text _mainTitleText;
        private Button _homeNavButton;
        private Button _garageNavButton;
        private Button _regionNavButton;
        private GameObject _homePanel;
        private Text _homeSummaryText;
        private Button _homeStartButton;
        private GameObject _garagePanel;
        private readonly Text[] _upgradeRows = new Text[7];
        private readonly Button[] _upgradeButtons = new Button[7];
        private readonly Image[] _upgradeRowImages = new Image[7];
        private readonly Text[] _upgradeStateTexts = new Text[7];
        private Text _upgradeHoverText;
        private GameObject _regionPanel;
        private Text _regionText;
        private readonly Button[] _regionItemButtons = new Button[MetaProgressionConstants.RegionIds.Length];
        private readonly Text[] _regionItemTexts = new Text[MetaProgressionConstants.RegionIds.Length];
        private readonly Image[] _regionItemIcons = new Image[MetaProgressionConstants.RegionIds.Length];
        private readonly Text[] _regionItemStateTexts = new Text[MetaProgressionConstants.RegionIds.Length];
        private Text _regionDetailText;
        private Button _unlockButton;
        private Text _unlockButtonText;
        private Button _nextRegionButton;
        private string _pendingUnlockRegionId;
        private string _inspectedRegionId;
        private CanvasScaler _canvasScaler;
        private RectTransform _mainPanelRect;
        private RectTransform _headerBarRect;
        private RectTransform _contentRootRect;
        private RectTransform _promptPanelRect;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Sprite _panelSkinSprite;
        private Sprite _buttonSkinSprite;
        private Sprite _buttonAccentSkinSprite;
        private Sprite _regionLockedIconSprite;
        private Sprite _regionUnlockableIconSprite;
        private Sprite _regionOpenIconSprite;
        private Sprite _regionSelectedIconSprite;

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

            if (_sceneUiOnlyMode)
            {
                EnsureSceneBasicUi();
                return;
            }

            EnsureRefs();
            UpdateResponsiveLayoutIfNeeded();
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
            _sceneUiOnlyMode = false;
            DestroyUi();
            DestroySceneBasicUi();
        }

        internal void OnTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _isLobby = false;
            _sceneUiOnlyMode = false;
            DestroyUi();
            DestroySceneBasicUi();
        }

        internal void OnTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleScene(evt.SceneName);
        }

        internal void OnMetaDirty()
        {
            if (_sceneUiOnlyMode)
            {
                return;
            }

            RefreshMeta();
        }

        private void HandleScene(string sceneName)
        {
            bool shouldLobby = sceneName == SceneNames.LobbyScene;
            if (_isLobby == shouldLobby)
            {
                if (_isLobby)
                {
                    _sceneUiOnlyMode = DetectSceneUiOnlyMode();
                    if (_sceneUiOnlyMode)
                    {
                        DestroyUi();
                        EnsureSceneBasicUi();
                        return;
                    }

                    DestroySceneBasicUi();
                    EnsureRefs();
                    EnsureUi();
                }

                return;
            }

            _isLobby = shouldLobby;
            _sceneUiOnlyMode = false;
            _zoneCount = 0;
            _activeZone = null;
            if (_isLobby)
            {
                _sceneUiOnlyMode = DetectSceneUiOnlyMode();
                if (_sceneUiOnlyMode)
                {
                    DestroyUi();
                    EnsureSceneBasicUi();
                    return;
                }

                DestroySceneBasicUi();
                EnsureRefs();
                EnsureUi();
                return;
            }

            DestroyUi();
            DestroySceneBasicUi();
        }

        private static bool DetectSceneUiOnlyMode()
        {
            if (GameObject.Find(SceneUiRootName) != null)
            {
                return true;
            }

            if (Object.FindAnyObjectByType<LobbyAvatarController>() != null)
            {
                return false;
            }

            if (Object.FindAnyObjectByType<LobbyInteractZone>() != null)
            {
                return false;
            }

            return true;
        }

        private void EnsureSceneBasicUi()
        {
            if (_sceneUiRoot == null)
            {
                _sceneUiRoot = GameObject.Find(SceneUiRootName);
            }

            if (_sceneUiRoot != null)
            {
                CacheSceneBasicUiRefs();
                BindSceneBasicUiButtons();
                if (_sceneUiStatusText != null && string.IsNullOrWhiteSpace(_sceneUiStatusText.text))
                {
                    _sceneUiStatusText.text = "Ready";
                }

                return;
            }

            UiEventSystemBootstrap.EnsureNow();

            Font font = ResolveLobbyFont();
            _sceneUiRoot = new GameObject(SceneUiRootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _sceneUiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = _sceneUiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = _sceneUiRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.SetParent(rootRect, false);
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.05f, 0.07f, 0.10f, 1f);
            backdropImage.raycastTarget = false;

            GameObject panel = new GameObject(SceneUiPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(rootRect, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -12f);
            panelRect.sizeDelta = new Vector2(560f, 360f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.10f, 0.13f, 0.18f, 0.95f);
            panelImage.raycastTarget = false;
            _sceneUiPanel = panel;

            Text titleText = CreateText(
                panelRect,
                font,
                54,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -40f),
                new Vector2(500f, 72f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Color(1f, 0.91f, 0.25f, 1f),
                "DELIVERY RUN");
            titleText.gameObject.name = "TitleText";

            Text hintText = CreateText(
                panelRect,
                font,
                22,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -100f),
                new Vector2(420f, 36f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Color(0.87f, 0.93f, 1f, 1f),
                "Simple Lobby UI");
            hintText.gameObject.name = "HintText";

            Text statusText = CreateText(
                panelRect,
                font,
                18,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -8f),
                new Vector2(420f, 30f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Color(0.74f, 0.88f, 1f, 1f),
                "Waiting for Core...");
            statusText.gameObject.name = StatusTextName;
            _sceneUiStatusText = statusText;

            Button startButton = CreateButton(panelRect, font, "START RUN", new Vector2(0f, 90f), new Vector2(260f, 58f), true);
            startButton.gameObject.name = StartButtonName;
            _sceneUiStartButton = startButton;

            Button exitButton = CreateButton(panelRect, font, "EXIT", new Vector2(0f, 26f), new Vector2(260f, 52f));
            exitButton.gameObject.name = ExitButtonName;
            _sceneUiExitButton = exitButton;
            BindSceneBasicUiButtons();
            SetSceneBasicStatus("Ready");
        }

        private void DestroySceneBasicUi()
        {
            UnbindSceneBasicUiButtons();
            _sceneUiPanel = null;
            _sceneUiStartButton = null;
            _sceneUiExitButton = null;
            _sceneUiStatusText = null;

            if (_sceneUiRoot == null)
            {
                return;
            }

            Object.Destroy(_sceneUiRoot);
            _sceneUiRoot = null;
        }

        private void CacheSceneBasicUiRefs()
        {
            if (_sceneUiRoot == null)
            {
                return;
            }

            if (_sceneUiPanel == null)
            {
                Transform panel = FindDescendantByName(_sceneUiRoot.transform, SceneUiPanelName);
                _sceneUiPanel = panel != null ? panel.gameObject : null;
            }

            if (_sceneUiStatusText == null)
            {
                Transform status = FindDescendantByName(_sceneUiRoot.transform, StatusTextName);
                _sceneUiStatusText = status != null ? status.GetComponent<Text>() : null;
            }

            if (_sceneUiStartButton == null)
            {
                Transform start = FindDescendantByName(_sceneUiRoot.transform, StartButtonName);
                _sceneUiStartButton = start != null ? start.GetComponent<Button>() : null;
            }

            if (_sceneUiExitButton == null)
            {
                Transform exit = FindDescendantByName(_sceneUiRoot.transform, ExitButtonName);
                _sceneUiExitButton = exit != null ? exit.GetComponent<Button>() : null;
            }
        }

        private void BindSceneBasicUiButtons()
        {
            if (_sceneUiStartButton != null)
            {
                _sceneUiStartButton.onClick.RemoveListener(OnSceneBasicStartClicked);
                _sceneUiStartButton.onClick.AddListener(OnSceneBasicStartClicked);
            }

            if (_sceneUiExitButton != null)
            {
                _sceneUiExitButton.onClick.RemoveListener(OnSceneBasicExitClicked);
                _sceneUiExitButton.onClick.AddListener(OnSceneBasicExitClicked);
            }
        }

        private void UnbindSceneBasicUiButtons()
        {
            if (_sceneUiStartButton != null)
            {
                _sceneUiStartButton.onClick.RemoveListener(OnSceneBasicStartClicked);
            }

            if (_sceneUiExitButton != null)
            {
                _sceneUiExitButton.onClick.RemoveListener(OnSceneBasicExitClicked);
            }
        }

        private void SetSceneBasicStatus(string message)
        {
            if (_sceneUiStatusText != null)
            {
                _sceneUiStatusText.text = message ?? string.Empty;
            }
        }

        private void OnSceneBasicStartClicked()
        {
            SetSceneBasicStatus("Loading run...");
            OnStartRunClicked();
        }

        private void OnSceneBasicExitClicked()
        {
            OnExitClicked();
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            if (string.Equals(root.name, targetName, System.StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindDescendantByName(child, targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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

            switch (zone.InteractType)
            {
                case LobbyInteractType.StartDelivery:
                    OnStartRunClicked();
                    break;
                case LobbyInteractType.OpenGaragePanel:
                    PlayUiClick();
                    SetView(LobbyViewMode.Garage);
                    break;
                case LobbyInteractType.OpenRegionPanel:
                    PlayUiClick();
                    SetView(LobbyViewMode.Region);
                    break;
                case LobbyInteractType.OpenSettingsPanel:
                    PlayUiClick();
                    SetView(LobbyViewMode.Home);
                    break;
                case LobbyInteractType.ExitGame:
                    OnExitClicked();
                    break;
            }
        }

        private void OnStartRunClicked()
        {
            PlayUiClick();
            _events.Publish(new StartRunRequested());
        }

        private void OnExitClicked()
        {
            PlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnUpgradeClicked(int index)
        {
            if (index < 0 || index >= UpgradeIds.Length)
            {
                return;
            }

            PlayUiClick();
            _events.Publish(new PermanentUpgradePurchaseRequested { UpgradeId = UpgradeIds[index] });
            ShowUpgradeHover(index);
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

        private void OnRegionItemClicked(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return;
            }

            PlayUiClick();
            _inspectedRegionId = regionId;
            RefreshMeta();
        }

        private void OnRegionItemHover(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || _regionDetailText == null || _meta == null)
            {
                return;
            }

            _regionDetailText.text = BuildRegionDetailText(regionId, out _, out _);
        }

        private void OnRegionItemHoverExit()
        {
            if (string.IsNullOrEmpty(_inspectedRegionId))
            {
                _inspectedRegionId = _meta != null ? _meta.SelectedRegionId : MetaProgressionConstants.RegionIds[0];
            }

            RefreshMeta();
        }

        private void OnUpgradeHoverEnter(int index)
        {
            ShowUpgradeHover(index);
        }

        private void OnUpgradeHoverExit()
        {
            if (_upgradeHoverText == null)
            {
                return;
            }

            _upgradeHoverText.text = "Hover an UPGRADE button to preview Lv change and real stat values.";
        }

        private void OnNextRegionClicked()
        {
            PlayUiClick();
            _events.Publish(new SelectNextRegionRequested());
            if (_meta != null)
            {
                _inspectedRegionId = _meta.SelectedRegionId;
            }
            RefreshMeta();
        }

        private void RefreshPrompt()
        {
            if (_promptText == null)
            {
                return;
            }

            if (_activeZone != null)
            {
                _promptText.text = _activeZone.PromptText + " [F]";
                return;
            }

            _promptText.text = "UI Lobby Mode";
        }

        private void SetView(LobbyViewMode view)
        {
            _currentView = view;

            if (_homePanel != null) _homePanel.SetActive(view == LobbyViewMode.Home);
            if (_garagePanel != null) _garagePanel.SetActive(view == LobbyViewMode.Garage);
            if (_regionPanel != null) _regionPanel.SetActive(view == LobbyViewMode.Region);
            UpdateMainTitle();
            SetButtonSelectedState(_homeNavButton, view == LobbyViewMode.Home);
            SetButtonSelectedState(_garageNavButton, view == LobbyViewMode.Garage);
            SetButtonSelectedState(_regionNavButton, view == LobbyViewMode.Region);
            RefreshMeta();
        }

        private void UpdateMainTitle()
        {
            if (_mainTitleText == null)
            {
                return;
            }

            if (_currentView == LobbyViewMode.Garage)
            {
                _mainTitleText.text = "GARAGE";
                return;
            }

            if (_currentView == LobbyViewMode.Region)
            {
                _mainTitleText.text = "REGION";
                return;
            }

            _mainTitleText.text = "LOBBY";
        }

        private void PlayUiClick()
        {
            if (_audio == null || _catalog == null)
            {
                return;
            }

            _audio.PlayUiClick(_catalog.UiClickKey);
        }

        private void ShowUpgradeHover(int index)
        {
            if (_upgradeHoverText == null || _meta == null || index < 0 || index >= UpgradeIds.Length)
            {
                return;
            }

            string upgradeId = UpgradeIds[index];
            string label = MetaProgressionConstants.UpgradeLabels[index];
            int current = _meta.GetUpgradeLevel(upgradeId);
            if (current >= 3)
            {
                _upgradeHoverText.text = label + " is already MAX (Lv.3).";
                return;
            }

            int next = current + 1;
            int price = UpgradePrice(next);
            _upgradeHoverText.text =
                label + " Upgrade Preview\n" +
                "Lv." + current + " -> Lv." + next + "\n" +
                BuildUpgradeValueDeltaText(index, current, next) + "\n" +
                "Cost: $" + price;
        }

        private string BuildUpgradeValueDeltaText(int index, int currentLevel, int nextLevel)
        {
            string upgradeId = UpgradeIds[index];
            return MetaUpgradePreviewUtil.BuildUpgradeValueDeltaText(
                upgradeId,
                currentLevel,
                nextLevel,
                GetUpgradeLevelForPreview);
        }

        private int GetUpgradeLevelForPreview(string upgradeId)
        {
            if (_meta == null || string.IsNullOrEmpty(upgradeId))
            {
                return 0;
            }

            return _meta.GetUpgradeLevel(upgradeId);
        }
    }
}
