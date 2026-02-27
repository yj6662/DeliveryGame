using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiLobbyManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private static readonly string[] RegionIds =
        {
            "central",
            "rushdistrict",
            "frostlands",
            "hillcrest",
            "stormcoast",
            "oldtown"
        };

        private static readonly string[] UpgradeIds =
        {
            "bike_speed",
            "bike_turn",
            "bike_accel",
            "music_luck",
            "bike_grip",
            "bike_brake",
            "reward_bonus"
        };

        private UiPrefabCatalogSO _catalog;
        private AudioManager _audioManager;
        private MetaProgressionService _metaService;

        private GameObject _root;
        private GameObject _settingsPanel;
        private Slider _bgmSlider;
        private Slider _uiSlider;
        private Text _metaCashText;
        private Text _metaRegionText;
        private Text _metaSelectedRegionText;
        private Text _metaUpgradeText;

        private bool _isLobbyScene;
        private float _scenePollElapsed;

        private Camera _lobbyCamera;
        private bool _createdLobbyCamera;

        public override string Name => nameof(UiLobbyManager);
        public override int InitOrder => 33;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            Services.TryGet(out _audioManager);
            Services.TryGet(out _metaService);

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<MetaBalanceChanged>(Events, OnMetaBalanceChanged);
            Subs.Add<RegionUnlocked>(Events, OnRegionUnlocked);
            Subs.Add<SelectedRegionChanged>(Events, OnSelectedRegionChanged);
            Subs.Add<PermanentUpgradeChanged>(Events, OnPermanentUpgradeChanged);

            HandleSceneChanged(SceneManager.GetActiveScene().name);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollElapsed += unscaledDeltaTime;
            if (_scenePollElapsed < ScenePollInterval)
            {
                return;
            }

            _scenePollElapsed = 0f;
            HandleSceneChanged(SceneManager.GetActiveScene().name);
        }

        protected override void OnShutdown()
        {
            DestroyLobbyUi();
            CleanupLobbyCamera();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _isLobbyScene = false;
            DestroyLobbyUi();
            CleanupLobbyCamera();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }

        private void OnMetaBalanceChanged(MetaBalanceChanged evt)
        {
            RefreshMetaTexts();
        }

        private void OnRegionUnlocked(RegionUnlocked evt)
        {
            RefreshMetaTexts();
        }

        private void OnSelectedRegionChanged(SelectedRegionChanged evt)
        {
            RefreshMetaTexts();
        }

        private void OnPermanentUpgradeChanged(PermanentUpgradeChanged evt)
        {
            RefreshMetaTexts();
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool shouldShow = sceneName == SceneNames.LobbyScene;
            if (_isLobbyScene == shouldShow)
            {
                if (shouldShow)
                {
                    EnsureLobbyUi();
                    EnsureLobbyCamera();
                }

                return;
            }

            _isLobbyScene = shouldShow;
            if (_isLobbyScene)
            {
                EnsureLobbyUi();
                EnsureLobbyCamera();
                return;
            }

            DestroyLobbyUi();
            CleanupLobbyCamera();
        }

        private void EnsureLobbyUi()
        {
            if (_root != null)
            {
                return;
            }

            UiEventSystemBootstrap.EnsureNow();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _root = new GameObject("LobbyUiRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject panelGo = new GameObject("LobbyPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.SetParent(rootRect, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -24f);
            panelRect.sizeDelta = new Vector2(720f, 430f);

            Image panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);

            Text title = CreateText("Title", panelRect, font, 64, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.22f, 1f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            title.rectTransform.sizeDelta = new Vector2(0f, 90f);
            title.text = "DELIVERY RUN";

            Text subtitle = CreateText("Subtitle", panelRect, font, 22, TextAnchor.UpperCenter, new Color(0.88f, 0.92f, 0.98f, 0.95f));
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -112f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 70f);
            subtitle.text = "Space: Accept Order  |  F: Interact";

            _metaCashText = CreateText("MetaCashText", panelRect, font, 24, TextAnchor.MiddleCenter, new Color(0.98f, 0.94f, 0.62f, 1f));
            _metaCashText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _metaCashText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _metaCashText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _metaCashText.rectTransform.anchoredPosition = new Vector2(0f, -156f);
            _metaCashText.rectTransform.sizeDelta = new Vector2(0f, 36f);

            _metaRegionText = CreateText("MetaRegionText", panelRect, font, 18, TextAnchor.MiddleCenter, new Color(0.85f, 0.92f, 1f, 0.98f));
            _metaRegionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _metaRegionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _metaRegionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _metaRegionText.rectTransform.anchoredPosition = new Vector2(0f, -188f);
            _metaRegionText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            _metaSelectedRegionText = CreateText("MetaSelectedRegionText", panelRect, font, 18, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f, 0.98f));
            _metaSelectedRegionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _metaSelectedRegionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _metaSelectedRegionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _metaSelectedRegionText.rectTransform.anchoredPosition = new Vector2(0f, -214f);
            _metaSelectedRegionText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            _metaUpgradeText = CreateText("MetaUpgradeText", panelRect, font, 17, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f, 0.92f));
            _metaUpgradeText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _metaUpgradeText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _metaUpgradeText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _metaUpgradeText.rectTransform.anchoredPosition = new Vector2(0f, -240f);
            _metaUpgradeText.rectTransform.sizeDelta = new Vector2(0f, 48f);

            Button startButton = CreateButton("StartRunButton", panelRect, font, "START RUN");
            RectTransform startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 158f);
            startRect.sizeDelta = new Vector2(250f, 58f);
            startButton.onClick.AddListener(OnStartRunClicked);

            Button settingsButton = CreateButton("SettingsButton", panelRect, font, "SETTINGS");
            RectTransform settingsRect = settingsButton.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0.5f, 0f);
            settingsRect.anchorMax = new Vector2(0.5f, 0f);
            settingsRect.pivot = new Vector2(0.5f, 0f);
            settingsRect.anchoredPosition = new Vector2(0f, 92f);
            settingsRect.sizeDelta = new Vector2(250f, 52f);
            settingsButton.onClick.AddListener(OnSettingsClicked);

            Button sectorButton = CreateButton("SectorButton", panelRect, font, "CHANGE SECTOR");
            RectTransform sectorRect = sectorButton.GetComponent<RectTransform>();
            sectorRect.anchorMin = new Vector2(0.5f, 0f);
            sectorRect.anchorMax = new Vector2(0.5f, 0f);
            sectorRect.pivot = new Vector2(0.5f, 0f);
            sectorRect.anchoredPosition = new Vector2(0f, 34f);
            sectorRect.sizeDelta = new Vector2(250f, 48f);
            sectorButton.onClick.AddListener(OnChangeSectorClicked);

            Button exitButton = CreateButton("ExitButton", panelRect, font, "EXIT");
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(0.5f, 0f);
            exitRect.anchorMax = new Vector2(0.5f, 0f);
            exitRect.pivot = new Vector2(0.5f, 0f);
            exitRect.anchoredPosition = new Vector2(0f, -20f);
            exitRect.sizeDelta = new Vector2(250f, 44f);
            exitButton.onClick.AddListener(OnExitClicked);

            BuildSettingsPanel(panelRect, font);
            RefreshMetaTexts();
        }

        private void BuildSettingsPanel(RectTransform parent, Font font)
        {
            GameObject panelGo = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 2f);
            panelRect.sizeDelta = new Vector2(560f, 220f);

            Image panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.14f, 0.2f, 0.97f);
            panelImage.raycastTarget = true;

            Text header = CreateText("Header", panelRect, font, 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.28f, 1f));
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            header.rectTransform.sizeDelta = new Vector2(0f, 40f);
            header.text = "SETTINGS";

            Text bgmLabel = CreateText("BgmLabel", panelRect, font, 18, TextAnchor.MiddleLeft, new Color(0.93f, 0.96f, 1f, 1f));
            bgmLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            bgmLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            bgmLabel.rectTransform.pivot = new Vector2(0f, 1f);
            bgmLabel.rectTransform.anchoredPosition = new Vector2(26f, -66f);
            bgmLabel.rectTransform.sizeDelta = new Vector2(130f, 26f);
            bgmLabel.text = "BGM";

            _bgmSlider = CreateSlider("BgmSlider", panelRect, new Vector2(170f, -70f), new Vector2(350f, 20f));
            _bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);

            Text uiLabel = CreateText("UiLabel", panelRect, font, 18, TextAnchor.MiddleLeft, new Color(0.93f, 0.96f, 1f, 1f));
            uiLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            uiLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            uiLabel.rectTransform.pivot = new Vector2(0f, 1f);
            uiLabel.rectTransform.anchoredPosition = new Vector2(26f, -116f);
            uiLabel.rectTransform.sizeDelta = new Vector2(130f, 26f);
            uiLabel.text = "UI SFX";

            _uiSlider = CreateSlider("UiSlider", panelRect, new Vector2(170f, -120f), new Vector2(350f, 20f));
            _uiSlider.onValueChanged.AddListener(OnUiSliderChanged);

            Button closeButton = CreateButton("CloseSettings", panelRect, font, "CLOSE");
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 18f);
            closeRect.sizeDelta = new Vector2(190f, 42f);
            closeButton.onClick.AddListener(() =>
            {
                TryPlayUiClick();
                SetSettingsVisible(false);
            });

            _settingsPanel = panelGo;
            SetSettingsVisible(false);

            if (_audioManager == null)
            {
                Services.TryGet(out _audioManager);
            }

            if (_audioManager != null)
            {
                _bgmSlider.SetValueWithoutNotify(_audioManager.GetBgmVolume01());
                _uiSlider.SetValueWithoutNotify(_audioManager.GetUiVolume01());
            }
            else
            {
                _bgmSlider.SetValueWithoutNotify(0.65f);
                _uiSlider.SetValueWithoutNotify(0.9f);
            }
        }

        private void EnsureLobbyCamera()
        {
            if (_lobbyCamera != null)
            {
                return;
            }

            Camera existingMain = Camera.main;
            if (existingMain != null)
            {
                _lobbyCamera = existingMain;
                _createdLobbyCamera = false;
                return;
            }

            Camera anyCamera = Object.FindAnyObjectByType<Camera>();
            if (anyCamera != null)
            {
                _lobbyCamera = anyCamera;
                if (!_lobbyCamera.CompareTag("MainCamera"))
                {
                    _lobbyCamera.tag = "MainCamera";
                }
                _createdLobbyCamera = false;
                return;
            }

            GameObject cameraObject = new GameObject("LobbyCamera");
            _lobbyCamera = cameraObject.AddComponent<Camera>();
            _lobbyCamera.clearFlags = CameraClearFlags.SolidColor;
            _lobbyCamera.backgroundColor = new Color(0.08f, 0.11f, 0.15f, 1f);
            _lobbyCamera.fieldOfView = 54f;
            _lobbyCamera.nearClipPlane = 0.01f;
            _lobbyCamera.farClipPlane = 600f;

            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 9f, -12f);
            cameraObject.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            cameraObject.AddComponent<AudioListener>();
            _createdLobbyCamera = true;
        }

        private void CleanupLobbyCamera()
        {
            if (_lobbyCamera == null)
            {
                _createdLobbyCamera = false;
                return;
            }

            if (_createdLobbyCamera)
            {
                Object.Destroy(_lobbyCamera.gameObject);
            }

            _lobbyCamera = null;
            _createdLobbyCamera = false;
        }

        private void OnStartRunClicked()
        {
            TryPlayUiClick();
            Events.Publish(new StartRunRequested());
        }

        private void OnSettingsClicked()
        {
            TryPlayUiClick();
            SetSettingsVisible(_settingsPanel == null || !_settingsPanel.activeSelf);
        }

        private void OnExitClicked()
        {
            TryPlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnChangeSectorClicked()
        {
            TryPlayUiClick();
            Events.Publish(new SelectNextRegionRequested());
        }

        private void OnBgmSliderChanged(float value)
        {
            if (_audioManager == null)
            {
                Services.TryGet(out _audioManager);
            }

            if (_audioManager != null)
            {
                _audioManager.SetBgmVolume01(value);
            }
        }

        private void OnUiSliderChanged(float value)
        {
            if (_audioManager == null)
            {
                Services.TryGet(out _audioManager);
            }

            if (_audioManager != null)
            {
                _audioManager.SetUiVolume01(value);
            }
        }

        private void SetSettingsVisible(bool visible)
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(visible);
            }
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

        private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.16f, 0.2f, 0.28f, 0.96f);

            Button button = go.GetComponent<Button>();

            Text text = CreateText("Label", rt, font, 22, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.82f, 1f));
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static Slider CreateSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Slider));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = anchoredPos;
            rootRect.sizeDelta = size;

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.SetParent(rootRect, false);
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(1f, 1f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(0.22f, 0.28f, 0.36f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(rootRect, false);
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(8f, 4f);
            fillAreaRect.offsetMax = new Vector2(-20f, -4f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(1f, 0.82f, 0.2f, 1f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.SetParent(rootRect, false);
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.SetParent(handleAreaRect, false);
            handleRect.sizeDelta = new Vector2(16f, size.y + 4f);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.96f, 0.97f, 0.99f, 1f);

            Slider slider = root.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.value = 1f;

            return slider;
        }

        private void DestroyLobbyUi()
        {
            _bgmSlider = null;
            _uiSlider = null;
            _settingsPanel = null;
            _metaCashText = null;
            _metaRegionText = null;
            _metaSelectedRegionText = null;
            _metaUpgradeText = null;

            if (_root == null)
            {
                return;
            }

            Object.Destroy(_root);
            _root = null;
        }

        private void RefreshMetaTexts()
        {
            if (_metaCashText == null || _metaRegionText == null || _metaSelectedRegionText == null || _metaUpgradeText == null)
            {
                return;
            }

            EnsureMetaService();
            if (_metaService == null)
            {
                _metaCashText.text = "Total Cash: $0";
                _metaRegionText.text = "Sectors: 1 / " + RegionIds.Length;
                _metaSelectedRegionText.text = "Selected: CENTRAL";
                _metaUpgradeText.text = "Upgrades: SPD L0 | TRN L0 | ACC L0 | LUCK L0";
                return;
            }

            int unlockedCount = 0;
            for (int i = 0; i < RegionIds.Length; i++)
            {
                if (_metaService.IsRegionUnlocked(RegionIds[i]))
                {
                    unlockedCount++;
                }
            }

            _metaCashText.text = "Total Cash: $" + _metaService.TotalCash;
            _metaRegionText.text = "Sectors: " + unlockedCount + " / " + RegionIds.Length;
            _metaSelectedRegionText.text = "Selected: " + FormatRegionName(_metaService.SelectedRegionId);
            _metaUpgradeText.text =
                "Upgrades: SPD L" + _metaService.GetUpgradeLevel(UpgradeIds[0]) +
                " | TRN L" + _metaService.GetUpgradeLevel(UpgradeIds[1]) +
                " | ACC L" + _metaService.GetUpgradeLevel(UpgradeIds[2]) +
                " | LUCK L" + _metaService.GetUpgradeLevel(UpgradeIds[3]);
        }

        private void EnsureMetaService()
        {
            if (_metaService != null)
            {
                return;
            }

            Services.TryGet(out _metaService);
        }

        private static string FormatRegionName(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return "CENTRAL";
            }

            string lower = regionId.ToLowerInvariant();
            if (lower == "rushdistrict") return "RUSH DISTRICT";
            if (lower == "frostlands") return "FROSTLANDS";
            if (lower == "hillcrest") return "HILLCREST";
            if (lower == "stormcoast") return "STORM COAST";
            if (lower == "oldtown") return "OLD TOWN";
            if (lower == "central") return "CENTRAL";

            return lower.ToUpperInvariant();
        }
    }
}
