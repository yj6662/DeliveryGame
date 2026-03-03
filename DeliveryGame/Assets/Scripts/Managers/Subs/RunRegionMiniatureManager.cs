using System;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RunRegionMiniatureManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.35f;
        private const float MiniatureOffsetX = 25000f;
        private const float MiniatureOffsetZ = 25000f;
        private const float MiniatureRaycastDistance = 900f;
        private const float HoverScaleMul = 1.08f;
        private const float SelectedScaleMul = 1.04f;
        private const float HoverPanelOffsetX = 16f;
        private const float HoverPanelOffsetY = -16f;
        private const float HoverPanelWidth = 468f;
        private const float HoverPanelMinHeight = 186f;
        private const float HoverPanelPadding = 16f;
        private const float HoverHeaderHeight = 44f;

        private const string CityRootName = "CityRoot";
        private const string MiniatureRootName = "RunRegionMiniatureRoot";
        private const string MiniatureCameraName = "RunRegionMiniatureCamera";
        private const string MiniatureHoverUiName = "RunRegionMiniatureHoverUI";
        private const string MiniatureSourcePrimaryScene = "RunScene_Placement";
        private const string LayoutResourcePath = "Bootstrap/RunRegionMiniatureLayout";
        private const string LayoutFallbackResourcePath = "RunRegionMiniatureLayout";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly Vector3[] FallbackLocalPositions =
        {
            new Vector3(-64f, 0f, -6f),
            new Vector3(-30f, 0f, -16f),
            new Vector3(2f, 0f, -10f),
            new Vector3(36f, 0f, -2f),
            new Vector3(-49f, 0f, 27f),
            new Vector3(-11f, 0f, 35f),
            new Vector3(30f, 0f, 30f)
        };

        private static readonly Vector3[] FallbackLocalEulers =
        {
            new Vector3(0f, 36f, 0f),
            new Vector3(0f, 24f, 0f),
            new Vector3(0f, 18f, 0f),
            new Vector3(0f, 14f, 0f),
            new Vector3(0f, 30f, 0f),
            new Vector3(0f, 20f, 0f),
            new Vector3(0f, 12f, 0f)
        };

        private readonly RegionEntry[] _entries = new RegionEntry[MetaProgressionConstants.RegionIds.Length];
        private readonly RaycastHit[] _raycastHits = new RaycastHit[16];
        private readonly MaterialPropertyBlock _materialPropertyBlock = new MaterialPropertyBlock();

        private MetaProgressionService _meta;
        private UiPrefabCatalogSO _uiCatalog;
        private RunRegionMiniatureLayoutSO _layout;
        private GameObject _miniatureRoot;
        private Camera _miniatureCamera;
        private GameObject _hoverUiRoot;
        private RectTransform _hoverPanelRect;
        private RectTransform _hoverHeaderRect;
        private Image _hoverPanelImage;
        private Image _hoverHeaderImage;
        private Image _hoverStatusIconImage;
        private Text _hoverTitleText;
        private Text _hoverText;
        private AsyncOperation _sourceSceneLoadOperation;
        private AsyncOperation _sourceSceneUnloadOperation;
        private bool _sourceSceneLoadedByMiniature;
        private string _loadedSourceSceneName;
        private Sprite _hoverPanelSprite;
        private Sprite _hoverHeaderSprite;
        private Sprite _hoverLockedIconSprite;
        private Sprite _hoverUnlockableIconSprite;
        private Sprite _hoverOpenIconSprite;

        private int _entryCount;
        private string _hoveredRegionId;
        private string _pendingUnlockRegionId;
        private float _scenePollElapsed;
        private bool _isLobby;
        private bool _missingCityRootLogged;

        public override string Name => nameof(RunRegionMiniatureManager);
        public override int InitOrder => 68;

        protected override void OnInitialize()
        {
            Services.TryGet(out _meta);
            _uiCatalog = UiPrefabCatalogLoader.LoadOrNull();
            EnsureHoverSkins();

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<RegionUnlocked>(Events, OnRegionUnlocked);
            Subs.Add<RegionUnlockFailed>(Events, OnRegionUnlockFailed);

            HandleScene(SceneManager.GetActiveScene().name, true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(ref _scenePollElapsed, ScenePollInterval, unscaledDeltaTime))
            {
                HandleScene(SceneManager.GetActiveScene().name, false);
            }

            if (!_isLobby)
            {
                return;
            }

            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            EnsureMiniatureRig();
            if (_miniatureRoot == null || _miniatureCamera == null || _entryCount <= 0)
            {
                return;
            }

            HandlePointer();
            RefreshEntryVisuals();
            RefreshHoverUi();
        }

        protected override void OnShutdown()
        {
            DestroyMiniatureRig();
            EnsureSourceSceneUnloaded();
            _layout = null;
            _meta = null;
            _uiCatalog = null;
            ReleaseHoverSkins();
            _isLobby = false;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _isLobby = false;
            DestroyMiniatureRig();
            EnsureSourceSceneUnloaded();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleScene(evt.SceneName, true);
        }

        private void OnRegionUnlocked(RegionUnlocked evt)
        {
            if (!_isLobby)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingUnlockRegionId) &&
                string.Equals(_pendingUnlockRegionId, evt.RegionId, StringComparison.Ordinal))
            {
                _pendingUnlockRegionId = null;
            }
        }

        private void OnRegionUnlockFailed(RegionUnlockFailed evt)
        {
            if (!_isLobby)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingUnlockRegionId) &&
                string.Equals(_pendingUnlockRegionId, evt.RegionId, StringComparison.Ordinal))
            {
                _pendingUnlockRegionId = null;
            }
        }

        private void HandleScene(string sceneName, bool forceRefresh)
        {
            bool shouldLobby = sceneName == SceneNames.LobbyScene;
            if (!shouldLobby)
            {
                _isLobby = false;
                DestroyMiniatureRig();
                EnsureSourceSceneUnloaded();
                return;
            }

            if (!_isLobby || forceRefresh)
            {
                _isLobby = true;
                _missingCityRootLogged = false;
                EnsureMiniatureRig();
                RefreshEntryVisuals();
            }
        }

        private void EnsureMiniatureRig()
        {
            if (_miniatureRoot == null)
            {
                _miniatureRoot = new GameObject(MiniatureRootName);
                _miniatureRoot.transform.position = new Vector3(MiniatureOffsetX, 0f, MiniatureOffsetZ);
            }

            if (_entryCount <= 0)
            {
                BuildMiniatureEntries();
            }

            if (_miniatureCamera == null)
            {
                CreateMiniatureCamera();
            }

            EnsureHoverUi();
        }

        private void DestroyMiniatureRig()
        {
            if (_miniatureCamera != null)
            {
                Object.Destroy(_miniatureCamera.gameObject);
                _miniatureCamera = null;
            }

            if (_miniatureRoot != null)
            {
                Object.Destroy(_miniatureRoot);
                _miniatureRoot = null;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i] = null;
            }

            _entryCount = 0;
            _hoveredRegionId = null;
            _pendingUnlockRegionId = null;
            _missingCityRootLogged = false;

            DestroyHoverUi();
        }
        private void BuildMiniatureEntries()
        {
            _entryCount = 0;

            if (_miniatureRoot == null)
            {
                return;
            }

            Transform cityRoot = ResolveCityRootSource();
            if (cityRoot == null)
            {
                if (!_missingCityRootLogged)
                {
                    _missingCityRootLogged = true;
                    Debug.LogWarning("[RunRegionMiniatureManager] CityRoot source not ready. Waiting for RunScene miniature source load.");
                }

                return;
            }

            _missingCityRootLogged = false;

            _layout = LoadLayout();
            string[] regionIds = MetaProgressionConstants.RegionIds;
            for (int i = 0; i < regionIds.Length; i++)
            {
                string regionId = regionIds[i];
                Transform source = FindSourceRegionRoot(cityRoot, regionId);
                if (source == null)
                {
                    continue;
                }

                GameObject clone = Object.Instantiate(source.gameObject, _miniatureRoot.transform);
                clone.name = "MiniRegion_" + regionId;
                StripCloneForMiniature(clone);
                ApplyRegionLayout(clone.transform, regionId, i);

                Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
                EnsureHitCollider(clone.transform, renderers);

                var entry = new RegionEntry
                {
                    RegionId = regionId,
                    Root = clone.transform,
                    BaseLocalScale = clone.transform.localScale,
                    Renderers = renderers
                };

                _entries[_entryCount] = entry;
                _entryCount++;
            }

            if (_entryCount <= 0)
            {
                Debug.LogWarning("[RunRegionMiniatureManager] No region roots were cloned for miniature view.");
                EnsureSourceSceneUnloaded();
                return;
            }

            EnsureSourceSceneUnloaded();
        }

        private Transform ResolveCityRootSource()
        {
            GameObject cityRootObject = GameObject.Find(CityRootName);
            if (cityRootObject != null)
            {
                return cityRootObject.transform;
            }

            EnsureSourceSceneLoaded();
            return null;
        }

        private void EnsureSourceSceneLoaded()
        {
            if (!_isLobby || _sourceSceneLoadOperation != null || _sourceSceneUnloadOperation != null)
            {
                return;
            }

            if (SceneIsLoaded(SceneNames.RunScene) || SceneIsLoaded(MiniatureSourcePrimaryScene))
            {
                return;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(MiniatureSourcePrimaryScene, LoadSceneMode.Additive);
            if (operation != null)
            {
                _loadedSourceSceneName = MiniatureSourcePrimaryScene;
            }
            else
            {
                operation = SceneManager.LoadSceneAsync(SceneNames.RunScene, LoadSceneMode.Additive);
                _loadedSourceSceneName = SceneNames.RunScene;
            }

            if (operation == null)
            {
                _loadedSourceSceneName = null;
                Debug.LogWarning("[RunRegionMiniatureManager] Failed to load miniature source scene.");
                return;
            }

            _sourceSceneLoadedByMiniature = true;
            _sourceSceneLoadOperation = operation;
            _sourceSceneLoadOperation.completed += OnSourceSceneLoadCompleted;
        }

        private void OnSourceSceneLoadCompleted(AsyncOperation _)
        {
            _sourceSceneLoadOperation = null;
            if (!_isLobby)
            {
                EnsureSourceSceneUnloaded();
            }
        }

        private void EnsureSourceSceneUnloaded()
        {
            if (_sourceSceneLoadOperation != null || _sourceSceneUnloadOperation != null || !_sourceSceneLoadedByMiniature)
            {
                return;
            }

            string sceneName = string.IsNullOrEmpty(_loadedSourceSceneName)
                ? SceneNames.RunScene
                : _loadedSourceSceneName;
            Scene loadedSourceScene = SceneManager.GetSceneByName(sceneName);
            if (!loadedSourceScene.IsValid() || !loadedSourceScene.isLoaded)
            {
                _sourceSceneLoadedByMiniature = false;
                _loadedSourceSceneName = null;
                return;
            }

            _sourceSceneUnloadOperation = SceneManager.UnloadSceneAsync(loadedSourceScene);
            if (_sourceSceneUnloadOperation == null)
            {
                _sourceSceneLoadedByMiniature = false;
                _loadedSourceSceneName = null;
                return;
            }

            _sourceSceneUnloadOperation.completed += OnSourceSceneUnloadCompleted;
        }

        private void OnSourceSceneUnloadCompleted(AsyncOperation _)
        {
            _sourceSceneUnloadOperation = null;
            _sourceSceneLoadedByMiniature = false;
            _loadedSourceSceneName = null;
        }

        private void EnsureHoverSkins()
        {
            if (_hoverPanelSprite != null || _hoverHeaderSprite != null ||
                _hoverLockedIconSprite != null || _hoverUnlockableIconSprite != null || _hoverOpenIconSprite != null)
            {
                return;
            }

            Texture2D panelTexture = _uiCatalog != null ? _uiCatalog.LobbyPanelTexture : null;
            Texture2D headerTexture = _uiCatalog != null ? _uiCatalog.LobbyButtonAccentTexture : null;
            Texture2D lockedIconTexture = _uiCatalog != null ? _uiCatalog.LobbyRegionLockedIconTexture : null;
            Texture2D unlockableIconTexture = _uiCatalog != null ? _uiCatalog.LobbyRegionUnlockableIconTexture : null;
            Texture2D openIconTexture = _uiCatalog != null ? _uiCatalog.LobbyRegionOpenIconTexture : null;

            _hoverPanelSprite = CreateSpriteFromTexture(panelTexture, new Vector4(28f, 28f, 28f, 28f));
            _hoverHeaderSprite = CreateSpriteFromTexture(headerTexture, new Vector4(22f, 22f, 22f, 22f));
            _hoverLockedIconSprite = CreateSpriteFromTexture(lockedIconTexture, Vector4.zero);
            _hoverUnlockableIconSprite = CreateSpriteFromTexture(unlockableIconTexture, Vector4.zero);
            _hoverOpenIconSprite = CreateSpriteFromTexture(openIconTexture, Vector4.zero);
        }

        private void ReleaseHoverSkins()
        {
            if (_hoverPanelSprite != null)
            {
                Object.Destroy(_hoverPanelSprite);
                _hoverPanelSprite = null;
            }

            if (_hoverHeaderSprite != null)
            {
                Object.Destroy(_hoverHeaderSprite);
                _hoverHeaderSprite = null;
            }

            if (_hoverLockedIconSprite != null)
            {
                Object.Destroy(_hoverLockedIconSprite);
                _hoverLockedIconSprite = null;
            }

            if (_hoverUnlockableIconSprite != null)
            {
                Object.Destroy(_hoverUnlockableIconSprite);
                _hoverUnlockableIconSprite = null;
            }

            if (_hoverOpenIconSprite != null)
            {
                Object.Destroy(_hoverOpenIconSprite);
                _hoverOpenIconSprite = null;
            }
        }

        private static Sprite CreateSpriteFromTexture(Texture2D texture, Vector4 border)
        {
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                border);
        }

        private void EnsureHoverUi()
        {
            if (_hoverUiRoot != null)
            {
                return;
            }

            EnsureHoverSkins();

            var root = new GameObject(
                MiniatureHoverUiName,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 96;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _hoverPanelRect = panel.GetComponent<RectTransform>();
            _hoverPanelRect.SetParent(rootRect, false);
            _hoverPanelRect.anchorMin = Vector2.zero;
            _hoverPanelRect.anchorMax = Vector2.zero;
            _hoverPanelRect.pivot = Vector2.zero;
            _hoverPanelRect.sizeDelta = new Vector2(HoverPanelWidth, HoverPanelMinHeight);

            _hoverPanelImage = panel.GetComponent<Image>();
            if (_hoverPanelSprite != null)
            {
                _hoverPanelImage.sprite = _hoverPanelSprite;
                _hoverPanelImage.type = Image.Type.Sliced;
                _hoverPanelImage.color = new Color(1f, 1f, 1f, 0.98f);
            }
            else
            {
                _hoverPanelImage.color = new Color(0.94f, 0.96f, 0.99f, 0.96f);
            }
            _hoverPanelImage.raycastTarget = false;

            GameObject header = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _hoverHeaderRect = header.GetComponent<RectTransform>();
            _hoverHeaderRect.SetParent(_hoverPanelRect, false);
            _hoverHeaderRect.anchorMin = new Vector2(0f, 1f);
            _hoverHeaderRect.anchorMax = new Vector2(1f, 1f);
            _hoverHeaderRect.pivot = new Vector2(0.5f, 1f);
            _hoverHeaderRect.anchoredPosition = Vector2.zero;
            _hoverHeaderRect.sizeDelta = new Vector2(0f, HoverHeaderHeight);

            _hoverHeaderImage = header.GetComponent<Image>();
            if (_hoverHeaderSprite != null)
            {
                _hoverHeaderImage.sprite = _hoverHeaderSprite;
                _hoverHeaderImage.type = Image.Type.Sliced;
                _hoverHeaderImage.color = new Color(0.99f, 0.84f, 0.35f, 0.98f);
            }
            else
            {
                _hoverHeaderImage.color = new Color(0.99f, 0.84f, 0.35f, 0.98f);
            }
            _hoverHeaderImage.raycastTarget = false;

            GameObject statusIcon = new GameObject("StatusIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform statusRect = statusIcon.GetComponent<RectTransform>();
            statusRect.SetParent(_hoverHeaderRect, false);
            statusRect.anchorMin = new Vector2(0f, 0.5f);
            statusRect.anchorMax = new Vector2(0f, 0.5f);
            statusRect.pivot = new Vector2(0f, 0.5f);
            statusRect.anchoredPosition = new Vector2(12f, 0f);
            statusRect.sizeDelta = new Vector2(20f, 20f);

            _hoverStatusIconImage = statusIcon.GetComponent<Image>();
            _hoverStatusIconImage.raycastTarget = false;
            _hoverStatusIconImage.color = Color.white;

            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.SetParent(_hoverHeaderRect, false);
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(38f, 0f);
            titleRect.offsetMax = new Vector2(-10f, 0f);

            _hoverTitleText = titleObj.GetComponent<Text>();
            _hoverTitleText.font = ResolveHoverFont();
            _hoverTitleText.fontSize = 20;
            _hoverTitleText.fontStyle = FontStyle.Bold;
            _hoverTitleText.alignment = TextAnchor.MiddleLeft;
            _hoverTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hoverTitleText.verticalOverflow = VerticalWrapMode.Overflow;
            _hoverTitleText.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            _hoverTitleText.raycastTarget = false;

            GameObject textObj = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.SetParent(_hoverPanelRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(HoverPanelPadding, HoverPanelPadding);
            textRect.offsetMax = new Vector2(-HoverPanelPadding, -(HoverHeaderHeight + 10f));

            _hoverText = textObj.GetComponent<Text>();
            _hoverText.font = ResolveHoverFont();
            _hoverText.fontSize = 17;
            _hoverText.alignment = TextAnchor.UpperLeft;
            _hoverText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hoverText.verticalOverflow = VerticalWrapMode.Overflow;
            _hoverText.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            _hoverText.lineSpacing = 1.05f;
            _hoverText.raycastTarget = false;

            _hoverUiRoot = root;
            SetHoverUiVisible(false);
        }

        private void RefreshHoverUi()
        {
            if (_hoverPanelRect == null || _hoverText == null)
            {
                return;
            }

            if (!_isLobby || string.IsNullOrEmpty(_hoveredRegionId))
            {
                SetHoverUiVisible(false);
                return;
            }

            bool unlocked;
            bool canUnlockNow;
            string title;
            string text = BuildHoverPanelText(_hoveredRegionId, out unlocked, out canUnlockNow, out title);
            if (string.IsNullOrEmpty(text))
            {
                SetHoverUiVisible(false);
                return;
            }

            if (_hoverTitleText != null)
            {
                _hoverTitleText.text = string.IsNullOrEmpty(title) ? "REGION INFO" : title;
            }

            _hoverText.text = text;
            if (_hoverHeaderImage != null)
            {
                if (unlocked)
                {
                    _hoverHeaderImage.color = new Color(0.62f, 0.9f, 0.62f, 0.98f);
                }
                else if (canUnlockNow)
                {
                    _hoverHeaderImage.color = new Color(0.99f, 0.84f, 0.35f, 0.98f);
                }
                else
                {
                    _hoverHeaderImage.color = new Color(0.98f, 0.6f, 0.6f, 0.98f);
                }
            }

            if (_hoverStatusIconImage != null)
            {
                Sprite icon = unlocked
                    ? _hoverOpenIconSprite
                    : (canUnlockNow ? _hoverUnlockableIconSprite : _hoverLockedIconSprite);
                _hoverStatusIconImage.enabled = icon != null;
                _hoverStatusIconImage.sprite = icon;
                _hoverStatusIconImage.color = Color.white;
            }

            float contentWidth = HoverPanelWidth - (HoverPanelPadding * 2f);
            float preferredHeight = _hoverText.cachedTextGeneratorForLayout.GetPreferredHeight(
                text,
                _hoverText.GetGenerationSettings(new Vector2(contentWidth, 0f)));

            float panelHeight = Mathf.Max(HoverPanelMinHeight, preferredHeight + HoverHeaderHeight + (HoverPanelPadding * 2f) + 10f);
            _hoverPanelRect.sizeDelta = new Vector2(HoverPanelWidth, panelHeight);
            UpdateHoverPanelPosition();
            SetHoverUiVisible(true);
        }

        private string BuildHoverPanelText(string regionId, out bool unlocked, out bool canUnlockNow, out string title)
        {
            unlocked = false;
            canUnlockNow = false;
            title = string.Empty;

            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            if (_meta == null || string.IsNullOrEmpty(regionId))
            {
                return string.Empty;
            }

            string displayName = LobbyRegionMetaUtil.FormatRegionName(regionId);
            title = displayName;
            unlocked = _meta.IsRegionUnlocked(regionId);
            if (unlocked)
            {
                return "Status: OPEN\nClick: Already unlocked.";
            }

            RegionUnlockState state;
            if (!TryBuildUnlockState(regionId, out state))
            {
                return "Status: LOCKED" +
                       "\nUnlock data unavailable.";
            }

            if (!state.HasRule)
            {
                canUnlockNow = true;
                return "Status: UNLOCKABLE" +
                       "\nStarter region." +
                       "\nClick: Unlock now.";
            }

            canUnlockNow = state.CanUnlockNow;
            string previousState = state.PreviousRegionUnlocked ? "OK" : "X";
            string cashState = state.BestRunCash >= state.Rule.RequiredRunCash ? "OK" : "X";
            string ratingState = state.BestRunRating >= state.Rule.RequiredRunRating ? "OK" : "X";
            string costState = state.CanAfford ? "OK" : "X";

            return "Status: " + (state.CanUnlockNow ? "UNLOCKABLE" : "LOCKED") +
                   "\nPrev Region: " + LobbyRegionMetaUtil.FormatRegionName(state.Rule.PreviousRegionId) + " [" + previousState + "]" +
                   "\nBest Cash: $" + state.BestRunCash + " / $" + state.Rule.RequiredRunCash + " [" + cashState + "]" +
                   "\nBest Rating: " + state.BestRunRating.ToString("0.0") + " / " + state.Rule.RequiredRunRating.ToString("0.0") + " [" + ratingState + "]" +
                   "\nUnlock Cost: $" + state.Rule.UnlockCost + " [" + costState + "]" +
                   "\nResult: " + (state.CanUnlockNow ? "Click to unlock now." : BuildMissingReason(state));
        }

        private void UpdateHoverPanelPosition()
        {
            if (_hoverPanelRect == null)
            {
                return;
            }

            Vector2 panelSize = _hoverPanelRect.sizeDelta;
            float x = Input.mousePosition.x + HoverPanelOffsetX;
            float y = Input.mousePosition.y + HoverPanelOffsetY;

            if (x + panelSize.x > Screen.width - 8f)
            {
                x = Input.mousePosition.x - panelSize.x - HoverPanelOffsetX;
            }

            if (y + panelSize.y > Screen.height - 8f)
            {
                y = Screen.height - panelSize.y - 8f;
            }

            if (y < 8f)
            {
                y = 8f;
            }

            if (x < 8f)
            {
                x = 8f;
            }

            _hoverPanelRect.anchoredPosition = new Vector2(x, y);
        }

        private void SetHoverUiVisible(bool visible)
        {
            if (_hoverPanelRect == null)
            {
                return;
            }

            if (_hoverPanelRect.gameObject.activeSelf != visible)
            {
                _hoverPanelRect.gameObject.SetActive(visible);
            }
        }

        private void DestroyHoverUi()
        {
            _hoverHeaderRect = null;
            _hoverPanelRect = null;
            _hoverHeaderImage = null;
            _hoverStatusIconImage = null;
            _hoverTitleText = null;
            _hoverText = null;
            _hoverPanelImage = null;

            if (_hoverUiRoot != null)
            {
                Object.Destroy(_hoverUiRoot);
                _hoverUiRoot = null;
            }
        }

        private Font ResolveHoverFont()
        {
            if (_uiCatalog != null && _uiCatalog.LobbyPrimaryFont != null)
            {
                return _uiCatalog.LobbyPrimaryFont;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static bool SceneIsLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        private void CreateMiniatureCamera()
        {
            if (_miniatureRoot == null)
            {
                return;
            }

            GameObject cameraObject = new GameObject(MiniatureCameraName);
            cameraObject.transform.SetParent(_miniatureRoot.transform, false);
            _miniatureCamera = cameraObject.AddComponent<Camera>();

            RunRegionMiniatureLayoutSO.CameraLayout cameraLayout;
            GetCameraLayout(out cameraLayout);

            cameraObject.transform.localPosition = cameraLayout.LocalPosition;
            Vector3 centerLocal = ComputeRegionCenterLocal();
            Vector3 lookTargetWorld = _miniatureRoot.transform.TransformPoint(centerLocal + cameraLayout.LookAtOffset);
            cameraObject.transform.LookAt(lookTargetWorld, Vector3.up);

            Rect viewport = cameraLayout.Viewport;
            viewport.x = Mathf.Clamp01(viewport.x);
            viewport.y = Mathf.Clamp01(viewport.y);
            viewport.width = Mathf.Clamp(viewport.width, 0.12f, 1f - viewport.x);
            viewport.height = Mathf.Clamp(viewport.height, 0.12f, 1f - viewport.y);

            _miniatureCamera.fieldOfView = Mathf.Clamp(cameraLayout.FieldOfView, 15f, 60f);
            _miniatureCamera.nearClipPlane = 0.1f;
            _miniatureCamera.farClipPlane = 600f;
            _miniatureCamera.depth = 90f;
            _miniatureCamera.clearFlags = CameraClearFlags.SolidColor;
            _miniatureCamera.backgroundColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            _miniatureCamera.rect = viewport;
            _miniatureCamera.allowMSAA = false;
            _miniatureCamera.allowHDR = false;
            _miniatureCamera.useOcclusionCulling = false;
        }

        private void HandlePointer()
        {
            string hoveredRegion = ResolveHoveredRegionId();
            _hoveredRegionId = hoveredRegion;

            if (string.IsNullOrEmpty(hoveredRegion))
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            TryUnlockRegion(hoveredRegion);
        }

        private string ResolveHoveredRegionId()
        {
            if (_miniatureCamera == null)
            {
                return null;
            }

            Vector3 mouse = Input.mousePosition;
            if (!_miniatureCamera.pixelRect.Contains(mouse))
            {
                return null;
            }

            Ray ray = _miniatureCamera.ScreenPointToRay(mouse);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                MiniatureRaycastDistance,
                ~0,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                return null;
            }

            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastHits[i];
                if (hit.collider == null || hit.distance >= bestDistance)
                {
                    continue;
                }

                string regionId;
                if (!TryResolveRegionIdFromTransform(hit.collider.transform, out regionId))
                {
                    continue;
                }

                bestDistance = hit.distance;
                bestIndex = i;
            }

            if (bestIndex < 0)
            {
                return null;
            }

            string resolvedRegionId;
            if (!TryResolveRegionIdFromTransform(_raycastHits[bestIndex].collider.transform, out resolvedRegionId))
            {
                return null;
            }

            return resolvedRegionId;
        }

        private bool TryResolveRegionIdFromTransform(Transform source, out string regionId)
        {
            Transform current = source;
            while (current != null)
            {
                for (int i = 0; i < _entryCount; i++)
                {
                    RegionEntry entry = _entries[i];
                    if (entry != null && entry.Root == current)
                    {
                        regionId = entry.RegionId;
                        return true;
                    }
                }

                current = current.parent;
            }

            regionId = null;
            return false;
        }

        private void TryUnlockRegion(string regionId)
        {
            if (_meta == null || string.IsNullOrEmpty(regionId))
            {
                return;
            }

            if (_meta.IsRegionUnlocked(regionId))
            {
                return;
            }

            RegionUnlockState state;
            if (!TryBuildUnlockState(regionId, out state) || !state.HasRule)
            {
                Debug.LogWarning("[RunRegionMiniatureManager] Unlock rule not found: " + regionId);
                return;
            }

            if (!state.CanUnlockNow)
            {
                Debug.Log("[RunRegionMiniatureManager] Unlock blocked for " + regionId + ": " + BuildMissingReason(state));
                return;
            }

            _pendingUnlockRegionId = regionId;
            Events.Publish(new UnlockRegionRequested { RegionId = regionId });
        }
        private void RefreshEntryVisuals()
        {
            if (_entryCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _entryCount; i++)
            {
                RegionEntry entry = _entries[i];
                if (entry == null || entry.Root == null)
                {
                    continue;
                }

                bool unlocked = _meta == null || _meta.IsRegionUnlocked(entry.RegionId);
                bool selected = _meta != null && string.Equals(_meta.SelectedRegionId, entry.RegionId, StringComparison.Ordinal);
                bool hovered = string.Equals(_hoveredRegionId, entry.RegionId, StringComparison.Ordinal);
                bool pending = string.Equals(_pendingUnlockRegionId, entry.RegionId, StringComparison.Ordinal);

                bool canUnlockNow = false;
                if (!unlocked)
                {
                    RegionUnlockState state;
                    if (TryBuildUnlockState(entry.RegionId, out state))
                    {
                        canUnlockNow = state.CanUnlockNow;
                    }
                }

                float scaleMul = 1f;
                if (hovered)
                {
                    scaleMul = HoverScaleMul;
                }
                else if (selected)
                {
                    scaleMul = SelectedScaleMul;
                }

                entry.Root.localScale = entry.BaseLocalScale * scaleMul;

                Color tint = ResolveTint(unlocked, selected, hovered, canUnlockNow, pending);
                ApplyTint(entry.Renderers, tint);
            }
        }

        private bool TryBuildUnlockState(string regionId, out RegionUnlockState state)
        {
            state = default;
            if (_meta == null || string.IsNullOrEmpty(regionId))
            {
                return false;
            }

            RegionUnlockRule rule;
            if (!RegionProgressionCatalog.TryGetRule(regionId, out rule))
            {
                state = new RegionUnlockState
                {
                    HasRule = false
                };

                return true;
            }

            int bestCash = _meta.GetBestRunCash(rule.PreviousRegionId);
            float bestRating = _meta.GetBestRunRating(rule.PreviousRegionId);
            bool previousUnlocked = _meta.IsRegionUnlocked(rule.PreviousRegionId);
            bool meetsPerformance = previousUnlocked &&
                                    bestCash >= rule.RequiredRunCash &&
                                    bestRating >= rule.RequiredRunRating;
            bool canAfford = _meta.TotalCash >= rule.UnlockCost;

            state = new RegionUnlockState
            {
                HasRule = true,
                Rule = rule,
                BestRunCash = bestCash,
                BestRunRating = bestRating,
                PreviousRegionUnlocked = previousUnlocked,
                MeetsPerformance = meetsPerformance,
                CanAfford = canAfford,
                CanUnlockNow = meetsPerformance && canAfford
            };

            return true;
        }

        private static string BuildMissingReason(RegionUnlockState state)
        {
            if (!state.PreviousRegionUnlocked)
            {
                return "Previous region is still locked.";
            }

            if (!state.MeetsPerformance)
            {
                return "Best run cash/rating requirement is not met.";
            }

            if (!state.CanAfford)
            {
                return "Not enough cash for unlock cost.";
            }

            return "Unlock requirements are not met.";
        }

        private static Transform FindSourceRegionRoot(Transform cityRoot, string regionId)
        {
            if (cityRoot == null || string.IsNullOrEmpty(regionId))
            {
                return null;
            }

            string targetName = "Region_" + regionId;
            Transform direct = cityRoot.Find(targetName);
            if (direct != null)
            {
                return direct;
            }

            Transform[] transforms = cityRoot.GetComponentsInChildren<Transform>(true);
            Transform best = null;
            int bestRendererCount = -1;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    !string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int rendererCount = candidate.GetComponentsInChildren<Renderer>(true).Length;
                if (rendererCount > bestRendererCount)
                {
                    bestRendererCount = rendererCount;
                    best = candidate;
                }
            }

            return best;
        }

        private static void StripCloneForMiniature(GameObject clone)
        {
            if (clone == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                behaviour.enabled = false;
            }

            AudioSource[] audioSources = clone.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] != null)
                {
                    audioSources[i].enabled = false;
                }
            }

            AudioListener[] listeners = clone.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null)
                {
                    listeners[i].enabled = false;
                }
            }

            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void ApplyRegionLayout(Transform root, string regionId, int index)
        {
            if (root == null)
            {
                return;
            }

            RunRegionMiniatureLayoutSO.RegionLayout layout;
            if (_layout != null && _layout.TryGetRegionLayout(regionId, out layout))
            {
                root.localPosition = layout.LocalPosition;
                root.localEulerAngles = layout.LocalEuler;
                float localScale = layout.LocalScale <= 0f ? 0.055f : layout.LocalScale;
                root.localScale = Vector3.one * localScale;
                if (layout.BuildingChildLimit > 0)
                {
                    ApplyBuildingChildLimit(root, layout.BuildingChildLimit);
                }

                return;
            }

            Vector3 fallbackPosition;
            if (index >= 0 && index < FallbackLocalPositions.Length)
            {
                fallbackPosition = FallbackLocalPositions[index];
            }
            else
            {
                float row = Mathf.Floor(index / 4f);
                float col = index - (row * 4f);
                fallbackPosition = new Vector3(-64f + (col * 36f), 0f, -12f + (row * 34f));
            }

            Vector3 fallbackEuler = index >= 0 && index < FallbackLocalEulers.Length
                ? FallbackLocalEulers[index]
                : new Vector3(0f, 18f, 0f);

            root.localPosition = fallbackPosition;
            root.localEulerAngles = fallbackEuler;
            root.localScale = Vector3.one * 0.055f;
        }

        private static void ApplyBuildingChildLimit(Transform regionRoot, int childLimit)
        {
            if (regionRoot == null || childLimit <= 0)
            {
                return;
            }

            Transform buildings = FindDescendantByName(regionRoot, "BuildingsRoot");
            if (buildings == null)
            {
                return;
            }

            for (int i = 0; i < buildings.childCount; i++)
            {
                Transform child = buildings.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(i < childLimit);
                }
            }
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform nested = FindDescendantByName(child, targetName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void EnsureHitCollider(Transform root, Renderer[] renderers)
        {
            if (root == null)
            {
                return;
            }

            if (root.GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = root.InverseTransformPoint(bounds.center);
            collider.size = ToLocalSize(bounds.size, root.lossyScale);
        }

        private static Vector3 ToLocalSize(Vector3 worldSize, Vector3 lossyScale)
        {
            float x = Mathf.Abs(lossyScale.x) <= 0.0001f ? worldSize.x : worldSize.x / Mathf.Abs(lossyScale.x);
            float y = Mathf.Abs(lossyScale.y) <= 0.0001f ? worldSize.y : worldSize.y / Mathf.Abs(lossyScale.y);
            float z = Mathf.Abs(lossyScale.z) <= 0.0001f ? worldSize.z : worldSize.z / Mathf.Abs(lossyScale.z);
            return new Vector3(x, y, z);
        }

        private void ApplyTint(Renderer[] renderers, Color tint)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                _materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(_materialPropertyBlock);
                _materialPropertyBlock.SetColor(BaseColorId, tint);
                _materialPropertyBlock.SetColor(ColorId, tint);
                renderer.SetPropertyBlock(_materialPropertyBlock);
            }
        }

        private static Color ResolveTint(bool unlocked, bool selected, bool hovered, bool canUnlockNow, bool pending)
        {
            Color tint;
            if (unlocked)
            {
                tint = selected
                    ? new Color(0.68f, 0.90f, 1f)
                    : new Color(0.93f, 0.93f, 0.93f);
            }
            else if (pending)
            {
                tint = new Color(1f, 0.80f, 0.46f);
            }
            else if (canUnlockNow)
            {
                tint = new Color(0.96f, 0.88f, 0.52f);
            }
            else
            {
                tint = new Color(0.43f, 0.43f, 0.43f);
            }

            if (hovered)
            {
                tint = Color.Lerp(tint, Color.white, 0.15f);
            }

            return tint;
        }

        private RunRegionMiniatureLayoutSO LoadLayout()
        {
            if (_layout != null)
            {
                return _layout;
            }

            _layout = Resources.Load<RunRegionMiniatureLayoutSO>(LayoutResourcePath);
            if (_layout == null)
            {
                _layout = Resources.Load<RunRegionMiniatureLayoutSO>(LayoutFallbackResourcePath);
            }

            return _layout;
        }

        private void GetCameraLayout(out RunRegionMiniatureLayoutSO.CameraLayout layout)
        {
            if (LoadLayout() != null)
            {
                layout = _layout.Camera;
                return;
            }

            layout = new RunRegionMiniatureLayoutSO.CameraLayout
            {
                LocalPosition = new Vector3(0f, 86f, -144f),
                LookAtOffset = new Vector3(0f, 4f, 0f),
                FieldOfView = 27f,
                Viewport = new Rect(0.62f, 0.03f, 0.35f, 0.34f)
            };
        }

        private Vector3 ComputeRegionCenterLocal()
        {
            if (_entryCount <= 0)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            int valid = 0;
            for (int i = 0; i < _entryCount; i++)
            {
                RegionEntry entry = _entries[i];
                if (entry == null || entry.Root == null)
                {
                    continue;
                }

                sum += entry.Root.localPosition;
                valid++;
            }

            if (valid <= 0)
            {
                return Vector3.zero;
            }

            return sum / valid;
        }

        private sealed class RegionEntry
        {
            internal string RegionId;
            internal Transform Root;
            internal Vector3 BaseLocalScale;
            internal Renderer[] Renderers;
        }

        private struct RegionUnlockState
        {
            internal bool HasRule;
            internal RegionUnlockRule Rule;
            internal int BestRunCash;
            internal float BestRunRating;
            internal bool PreviousRegionUnlocked;
            internal bool MeetsPerformance;
            internal bool CanAfford;
            internal bool CanUnlockNow;
        }
    }
}
