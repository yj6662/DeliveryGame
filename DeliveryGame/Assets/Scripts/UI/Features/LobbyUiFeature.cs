using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;


namespace DeliveryRun.UI.Features
{
    internal sealed class LobbyUiFeature : UiFeatureBase
    {
        protected override void OnInitialize()
        {
            InitializeLobbyModule();
        }
        protected override void OnTick(float unscaledDeltaTime)
        {
            TickLobbyModule(unscaledDeltaTime);
        }
        protected override void OnShutdown()
        {
            ShutdownLobbyModule();
        }

        private const float LobbyScenePollInterval = 0.25f;
        private static readonly string[] LobbyRegionIds =
        {
            "central",
            "rushdistrict",
            "frostlands",
            "hillcrest",
            "stormcoast",
            "oldtown",
            "seaside"
        };

        private static readonly string[] LobbyUpgradeIds =
        {
            "bike_speed",
            "bike_turn",
            "bike_accel",
            "music_luck",
            "bike_grip",
            "bike_brake",
            "reward_bonus"
        };

        private AudioManager _lobbyAudioManager;
        private MetaProgressionService _lobbyMetaService;

        private GameObject _lobbyRoot;
        private GameObject _lobbySettingsPanel;
        private Slider _lobbyBgmSlider;
        private Slider _lobbyUiSlider;
        private Text _lobbyMetaCashText;
        private Text _lobbyMetaRegionText;
        private Text _lobbyMetaSelectedRegionText;
        private Text _lobbyMetaUpgradeText;
        private Text _lobbyMetaUnlockText;
        private Button _lobbyUnlockRegionButton;
        private Text _lobbyUnlockRegionButtonLabel;
        private string _lobbyPendingUnlockRegionId;

        private bool _lobbyModuleIsLobbyScene;
        private float _lobbyScenePollElapsed;
        private bool _lobbyModuleSuppressedByHub;

        private Camera _lobbyModuleCamera;
        private bool _lobbyModuleCreatedCamera;

        private void InitializeLobbyModule()
        {
            LobbyHubManager lobbyHubManager;
            if (Services.TryGet(out lobbyHubManager) && lobbyHubManager != null)
            {
                _lobbyModuleSuppressedByHub = true;
                return;
            }

            _lobbyModuleSuppressedByHub = false;
            Services.TryGet(out _lobbyAudioManager);
            Services.TryGet(out _lobbyMetaService);

            Subs.Add<SceneTransitionStarted>(Events, OnLobbySceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnLobbySceneTransitionCompleted);
            Subs.Add<MetaBalanceChanged>(Events, OnLobbyMetaBalanceChanged);
            Subs.Add<RegionUnlocked>(Events, OnLobbyRegionUnlocked);
            Subs.Add<RegionUnlockStatusChanged>(Events, OnLobbyRegionUnlockStatusChanged);
            Subs.Add<RegionUnlockFailed>(Events, OnLobbyRegionUnlockFailed);
            Subs.Add<SelectedRegionChanged>(Events, OnLobbySelectedRegionChanged);
            Subs.Add<PermanentUpgradeChanged>(Events, OnLobbyPermanentUpgradeChanged);

            HandleLobbySceneChanged(SceneManager.GetActiveScene().name);
        }

        private void TickLobbyModule(float unscaledDeltaTime)
        {
            if (_lobbyModuleSuppressedByHub)
            {
                return;
            }

            _lobbyScenePollElapsed += unscaledDeltaTime;
            if (_lobbyScenePollElapsed < LobbyScenePollInterval)
            {
                return;
            }

            _lobbyScenePollElapsed = 0f;
            HandleLobbySceneChanged(SceneManager.GetActiveScene().name);
        }

        private void ShutdownLobbyModule()
        {
            if (_lobbyModuleSuppressedByHub)
            {
                _lobbyModuleSuppressedByHub = false;
                return;
            }

            DestroyLobbyModuleUi();
            CleanupLobbyModuleCamera();
        }

        private void OnLobbySceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _lobbyModuleIsLobbyScene = false;
            DestroyLobbyModuleUi();
            CleanupLobbyModuleCamera();
        }

        private void OnLobbySceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleLobbySceneChanged(evt.SceneName);
        }

        private void OnLobbyMetaBalanceChanged(MetaBalanceChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyRegionUnlocked(RegionUnlocked evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbySelectedRegionChanged(SelectedRegionChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyRegionUnlockStatusChanged(RegionUnlockStatusChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyRegionUnlockFailed(RegionUnlockFailed evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void OnLobbyPermanentUpgradeChanged(PermanentUpgradeChanged evt)
        {
            RefreshLobbyMetaTexts();
        }

        private void HandleLobbySceneChanged(string sceneName)
        {
            bool shouldShow = sceneName == SceneNames.LobbyScene;
            if (_lobbyModuleIsLobbyScene == shouldShow)
            {
                if (shouldShow)
                {
                    EnsureLobbyModuleUi();
                    EnsureLobbyModuleCamera();
                }

                return;
            }

            _lobbyModuleIsLobbyScene = shouldShow;
            if (_lobbyModuleIsLobbyScene)
            {
                EnsureLobbyModuleUi();
                EnsureLobbyModuleCamera();
                return;
            }

            DestroyLobbyModuleUi();
            CleanupLobbyModuleCamera();
        }

        private void EnsureLobbyModuleUi()
        {
            if (_lobbyRoot != null)
            {
                return;
            }

            UiEventSystemBootstrap.EnsureNow();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _lobbyRoot = new GameObject("LobbyUiRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _lobbyRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            CanvasScaler scaler = _lobbyRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = _lobbyRoot.GetComponent<RectTransform>();
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

            Text title = CreateLobbyText("Title", panelRect, font, 64, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.22f, 1f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            title.rectTransform.sizeDelta = new Vector2(0f, 90f);
            title.text = "DELIVERY RUN";

            Text subtitle = CreateLobbyText("Subtitle", panelRect, font, 22, TextAnchor.UpperCenter, new Color(0.88f, 0.92f, 0.98f, 0.95f));
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -112f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 70f);
            subtitle.text = "Space: Accept Order  |  F: Interact";

            _lobbyMetaCashText = CreateLobbyText("MetaCashText", panelRect, font, 24, TextAnchor.MiddleCenter, new Color(0.98f, 0.94f, 0.62f, 1f));
            _lobbyMetaCashText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaCashText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaCashText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaCashText.rectTransform.anchoredPosition = new Vector2(0f, -156f);
            _lobbyMetaCashText.rectTransform.sizeDelta = new Vector2(0f, 36f);

            _lobbyMetaRegionText = CreateLobbyText("MetaRegionText", panelRect, font, 18, TextAnchor.MiddleCenter, new Color(0.85f, 0.92f, 1f, 0.98f));
            _lobbyMetaRegionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaRegionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaRegionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaRegionText.rectTransform.anchoredPosition = new Vector2(0f, -188f);
            _lobbyMetaRegionText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            _lobbyMetaSelectedRegionText = CreateLobbyText("MetaSelectedRegionText", panelRect, font, 18, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f, 0.98f));
            _lobbyMetaSelectedRegionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaSelectedRegionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaSelectedRegionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaSelectedRegionText.rectTransform.anchoredPosition = new Vector2(0f, -214f);
            _lobbyMetaSelectedRegionText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            _lobbyMetaUpgradeText = CreateLobbyText("MetaUpgradeText", panelRect, font, 17, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f, 0.92f));
            _lobbyMetaUpgradeText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaUpgradeText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaUpgradeText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaUpgradeText.rectTransform.anchoredPosition = new Vector2(0f, -240f);
            _lobbyMetaUpgradeText.rectTransform.sizeDelta = new Vector2(0f, 48f);

            _lobbyMetaUnlockText = CreateLobbyText("MetaUnlockText", panelRect, font, 16, TextAnchor.UpperCenter, new Color(0.94f, 0.96f, 1f, 0.95f));
            _lobbyMetaUnlockText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaUnlockText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaUnlockText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaUnlockText.rectTransform.anchoredPosition = new Vector2(0f, -286f);
            _lobbyMetaUnlockText.rectTransform.sizeDelta = new Vector2(0f, 58f);
            _lobbyMetaUnlockText.lineSpacing = 1.04f;

            Button startButton = CreateLobbyButton("StartRunButton", panelRect, font, "START RUN");
            RectTransform startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 158f);
            startRect.sizeDelta = new Vector2(250f, 58f);
            startButton.onClick.AddListener(OnLobbyStartRunClicked);

            Button settingsButton = CreateLobbyButton("SettingsButton", panelRect, font, "SETTINGS");
            RectTransform settingsRect = settingsButton.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0.5f, 0f);
            settingsRect.anchorMax = new Vector2(0.5f, 0f);
            settingsRect.pivot = new Vector2(0.5f, 0f);
            settingsRect.anchoredPosition = new Vector2(0f, 92f);
            settingsRect.sizeDelta = new Vector2(250f, 52f);
            settingsButton.onClick.AddListener(OnLobbySettingsClicked);

            Button sectorButton = CreateLobbyButton("SectorButton", panelRect, font, "CHANGE SECTOR");
            RectTransform sectorRect = sectorButton.GetComponent<RectTransform>();
            sectorRect.anchorMin = new Vector2(0.5f, 0f);
            sectorRect.anchorMax = new Vector2(0.5f, 0f);
            sectorRect.pivot = new Vector2(0.5f, 0f);
            sectorRect.anchoredPosition = new Vector2(0f, 34f);
            sectorRect.sizeDelta = new Vector2(250f, 48f);
            sectorButton.onClick.AddListener(OnLobbyChangeSectorClicked);

            _lobbyUnlockRegionButton = CreateLobbyButton("UnlockRegionButton", panelRect, font, "UNLOCK REGION");
            RectTransform unlockRect = _lobbyUnlockRegionButton.GetComponent<RectTransform>();
            unlockRect.anchorMin = new Vector2(0.5f, 0f);
            unlockRect.anchorMax = new Vector2(0.5f, 0f);
            unlockRect.pivot = new Vector2(0.5f, 0f);
            unlockRect.anchoredPosition = new Vector2(0f, -20f);
            unlockRect.sizeDelta = new Vector2(250f, 44f);
            _lobbyUnlockRegionButton.onClick.AddListener(OnLobbyUnlockRegionClicked);
            _lobbyUnlockRegionButtonLabel = _lobbyUnlockRegionButton.GetComponentInChildren<Text>();

            Button exitButton = CreateLobbyButton("ExitButton", panelRect, font, "EXIT");
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(0.5f, 0f);
            exitRect.anchorMax = new Vector2(0.5f, 0f);
            exitRect.pivot = new Vector2(0.5f, 0f);
            exitRect.anchoredPosition = new Vector2(0f, -72f);
            exitRect.sizeDelta = new Vector2(250f, 44f);
            exitButton.onClick.AddListener(OnLobbyExitClicked);

            BuildLobbySettingsPanel(panelRect, font);
            RefreshLobbyMetaTexts();
        }

        private void BuildLobbySettingsPanel(RectTransform parent, Font font)
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

            Text header = CreateLobbyText("Header", panelRect, font, 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.28f, 1f));
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            header.rectTransform.sizeDelta = new Vector2(0f, 40f);
            header.text = "SETTINGS";

            Text bgmLabel = CreateLobbyText("BgmLabel", panelRect, font, 18, TextAnchor.MiddleLeft, new Color(0.93f, 0.96f, 1f, 1f));
            bgmLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            bgmLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            bgmLabel.rectTransform.pivot = new Vector2(0f, 1f);
            bgmLabel.rectTransform.anchoredPosition = new Vector2(26f, -66f);
            bgmLabel.rectTransform.sizeDelta = new Vector2(130f, 26f);
            bgmLabel.text = "BGM";

            _lobbyBgmSlider = CreateLobbySlider("BgmSlider", panelRect, new Vector2(170f, -70f), new Vector2(350f, 20f));
            _lobbyBgmSlider.onValueChanged.AddListener(OnLobbyBgmSliderChanged);

            Text uiLabel = CreateLobbyText("UiLabel", panelRect, font, 18, TextAnchor.MiddleLeft, new Color(0.93f, 0.96f, 1f, 1f));
            uiLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            uiLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            uiLabel.rectTransform.pivot = new Vector2(0f, 1f);
            uiLabel.rectTransform.anchoredPosition = new Vector2(26f, -116f);
            uiLabel.rectTransform.sizeDelta = new Vector2(130f, 26f);
            uiLabel.text = "UI SFX";

            _lobbyUiSlider = CreateLobbySlider("UiSlider", panelRect, new Vector2(170f, -120f), new Vector2(350f, 20f));
            _lobbyUiSlider.onValueChanged.AddListener(OnLobbyUiSliderChanged);

            Button closeButton = CreateLobbyButton("CloseSettings", panelRect, font, "CLOSE");
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 18f);
            closeRect.sizeDelta = new Vector2(190f, 42f);
            closeButton.onClick.AddListener(() =>
            {
                TryPlayLobbyUiClick();
                SetLobbySettingsVisible(false);
            });

            _lobbySettingsPanel = panelGo;
            SetLobbySettingsVisible(false);

            if (_lobbyAudioManager == null)
            {
                Services.TryGet(out _lobbyAudioManager);
            }

            if (_lobbyAudioManager != null)
            {
                _lobbyBgmSlider.SetValueWithoutNotify(_lobbyAudioManager.GetBgmVolume01());
                _lobbyUiSlider.SetValueWithoutNotify(_lobbyAudioManager.GetUiVolume01());
            }
            else
            {
                _lobbyBgmSlider.SetValueWithoutNotify(0.65f);
                _lobbyUiSlider.SetValueWithoutNotify(0.9f);
            }
        }

        private void EnsureLobbyModuleCamera()
        {
            if (_lobbyModuleCamera != null)
            {
                return;
            }

            Camera existingMain = Camera.main;
            if (existingMain != null)
            {
                _lobbyModuleCamera = existingMain;
                _lobbyModuleCreatedCamera = false;
                return;
            }

            Camera anyCamera = Object.FindAnyObjectByType<Camera>();
            if (anyCamera != null)
            {
                _lobbyModuleCamera = anyCamera;
                if (!_lobbyModuleCamera.CompareTag("MainCamera"))
                {
                    _lobbyModuleCamera.tag = "MainCamera";
                }
                _lobbyModuleCreatedCamera = false;
                return;
            }

            GameObject cameraObject = new GameObject("LobbyCamera");
            _lobbyModuleCamera = cameraObject.AddComponent<Camera>();
            _lobbyModuleCamera.clearFlags = CameraClearFlags.SolidColor;
            _lobbyModuleCamera.backgroundColor = new Color(0.08f, 0.11f, 0.15f, 1f);
            _lobbyModuleCamera.fieldOfView = 54f;
            _lobbyModuleCamera.nearClipPlane = 0.01f;
            _lobbyModuleCamera.farClipPlane = 600f;

            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 9f, -12f);
            cameraObject.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            cameraObject.AddComponent<AudioListener>();
            _lobbyModuleCreatedCamera = true;
        }

        private void CleanupLobbyModuleCamera()
        {
            if (_lobbyModuleCamera == null)
            {
                _lobbyModuleCreatedCamera = false;
                return;
            }

            if (_lobbyModuleCreatedCamera)
            {
                Object.Destroy(_lobbyModuleCamera.gameObject);
            }

            _lobbyModuleCamera = null;
            _lobbyModuleCreatedCamera = false;
        }

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

        private static Text CreateLobbyText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
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

        private static Button CreateLobbyButton(string name, Transform parent, Font font, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.16f, 0.2f, 0.28f, 0.96f);

            Button button = go.GetComponent<Button>();

            Text text = CreateLobbyText("Label", rt, font, 22, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.82f, 1f));
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static Slider CreateLobbySlider(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
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

        private void RefreshLobbyMetaTexts()
        {
            if (_lobbyMetaCashText == null || _lobbyMetaRegionText == null || _lobbyMetaSelectedRegionText == null || _lobbyMetaUpgradeText == null || _lobbyMetaUnlockText == null)
            {
                return;
            }

            EnsureLobbyMetaService();
            if (_lobbyMetaService == null)
            {
                _lobbyMetaCashText.text = "Total Cash: $0";
                _lobbyMetaRegionText.text = "Sectors: 1 / " + LobbyRegionIds.Length;
                _lobbyMetaSelectedRegionText.text = "Selected: CENTRAL";
                _lobbyMetaUpgradeText.text = "Upgrades: SPD L0 | TRN L0 | ACC L0 | LUCK L0";
                _lobbyMetaUnlockText.text = "Unlock info unavailable.";
                _lobbyPendingUnlockRegionId = null;
                if (_lobbyUnlockRegionButton != null) _lobbyUnlockRegionButton.interactable = false;
                if (_lobbyUnlockRegionButtonLabel != null) _lobbyUnlockRegionButtonLabel.text = "UNLOCK REGION";
                return;
            }

            int unlockedCount = 0;
            for (int i = 0; i < LobbyRegionIds.Length; i++)
            {
                if (_lobbyMetaService.IsRegionUnlocked(LobbyRegionIds[i]))
                {
                    unlockedCount++;
                }
            }

            _lobbyMetaCashText.text = "Total Cash: $" + _lobbyMetaService.TotalCash;
            _lobbyMetaRegionText.text = "Sectors: " + unlockedCount + " / " + LobbyRegionIds.Length;
            _lobbyMetaSelectedRegionText.text = "Selected: " + FormatLobbyRegionName(_lobbyMetaService.SelectedRegionId);
            _lobbyMetaUpgradeText.text =
                "Upgrades: SPD L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[0]) +
                " | TRN L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[1]) +
                " | ACC L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[2]) +
                " | LUCK L" + _lobbyMetaService.GetUpgradeLevel(LobbyUpgradeIds[3]);

            RegionUnlockRule rule;
            if (!RegionProgressionCatalog.TryGetNextLockedRegion(_lobbyMetaService, out _lobbyPendingUnlockRegionId) ||
                !RegionProgressionCatalog.TryGetRule(_lobbyPendingUnlockRegionId, out rule))
            {
                _lobbyMetaUnlockText.text = "All sectors unlocked.";
                if (_lobbyUnlockRegionButton != null) _lobbyUnlockRegionButton.interactable = false;
                if (_lobbyUnlockRegionButtonLabel != null) _lobbyUnlockRegionButtonLabel.text = "ALL UNLOCKED";
                _lobbyPendingUnlockRegionId = null;
                return;
            }

            int bestCash = _lobbyMetaService.GetBestRunCash(rule.PreviousRegionId);
            float bestRating = _lobbyMetaService.GetBestRunRating(rule.PreviousRegionId);
            bool previousUnlocked = _lobbyMetaService.IsRegionUnlocked(rule.PreviousRegionId);
            bool meetsPerformance = previousUnlocked &&
                                    bestCash >= rule.RequiredRunCash &&
                                    bestRating >= rule.RequiredRunRating;
            bool canAfford = _lobbyMetaService.TotalCash >= rule.UnlockCost;
            bool canUnlock = meetsPerformance && canAfford;

            if (!previousUnlocked)
            {
                _lobbyMetaUnlockText.text =
                    "Unlock " + FormatLobbyRegionName(rule.RegionId) + ": clear " + FormatLobbyRegionName(rule.PreviousRegionId) + " first.";
            }
            else
            {
                _lobbyMetaUnlockText.text =
                    "Unlock " + FormatLobbyRegionName(rule.RegionId) +
                    " | Prev best $" + bestCash + " (need $" + rule.RequiredRunCash + ")" +
                    " | R " + bestRating.ToString("0.0") + " (need " + rule.RequiredRunRating.ToString("0.0") + ")" +
                    " | Cost $" + rule.UnlockCost;
            }

            if (_lobbyUnlockRegionButton != null)
            {
                _lobbyUnlockRegionButton.interactable = canUnlock;
            }

            if (_lobbyUnlockRegionButtonLabel != null)
            {
                if (canUnlock)
                {
                    _lobbyUnlockRegionButtonLabel.text = "UNLOCK " + FormatLobbyRegionName(rule.RegionId);
                }
                else if (!canAfford)
                {
                    _lobbyUnlockRegionButtonLabel.text = "NEED CASH $" + rule.UnlockCost;
                }
                else
                {
                    _lobbyUnlockRegionButtonLabel.text = "REQUIREMENTS LOCKED";
                }
            }
        }

        private void EnsureLobbyMetaService()
        {
            if (_lobbyMetaService != null)
            {
                return;
            }

            Services.TryGet(out _lobbyMetaService);
        }

        private static string FormatLobbyRegionName(string regionId)
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
            if (lower == "seaside") return "SEASIDE";
            if (lower == "central") return "CENTRAL";

            return lower.ToUpperInvariant();
        }
    }
}
