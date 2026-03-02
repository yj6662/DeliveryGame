using System;
using System.Text;
using DeliveryRun.Managers.Core;
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
        private const float FeedbackDuration = 2.1f;

        private const string CityRootName = "CityRoot";
        private const string MiniatureRootName = "RunRegionMiniatureRoot";
        private const string MiniatureCameraName = "RunRegionMiniatureCamera";
        private const string UiRootName = "RunRegionMiniatureUI";
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
        private RunRegionMiniatureLayoutSO _layout;
        private GameObject _miniatureRoot;
        private Camera _miniatureCamera;
        private Canvas _canvas;
        private Text _titleText;
        private Text _detailText;

        private int _entryCount;
        private string _hoveredRegionId;
        private string _pendingUnlockRegionId;
        private string _feedbackMessage;
        private float _feedbackUntil;
        private float _scenePollElapsed;
        private bool _isRunScene;
        private bool _missingCityRootLogged;

        public override string Name => nameof(RunRegionMiniatureManager);
        public override int InitOrder => 68;

        protected override void OnInitialize()
        {
            Services.TryGet(out _meta);

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<MetaBalanceChanged>(Events, OnMetaChanged);
            Subs.Add<SelectedRegionChanged>(Events, OnMetaChanged);
            Subs.Add<RegionUnlockStatusChanged>(Events, OnMetaChanged);
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

            if (!_isRunScene)
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
            RefreshInfoPanel();
        }

        protected override void OnShutdown()
        {
            DestroyMiniatureRig();
            _layout = null;
            _meta = null;
            _isRunScene = false;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (SceneNames.IsRunSceneLike(evt.To))
            {
                return;
            }

            _isRunScene = false;
            DestroyMiniatureRig();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleScene(evt.SceneName, true);
        }

        private void OnMetaChanged(MetaBalanceChanged evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            RefreshInfoPanel();
        }

        private void OnMetaChanged(SelectedRegionChanged evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            RefreshInfoPanel();
        }

        private void OnMetaChanged(RegionUnlockStatusChanged evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            RefreshInfoPanel();
        }

        private void OnRegionUnlocked(RegionUnlocked evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingUnlockRegionId) &&
                string.Equals(_pendingUnlockRegionId, evt.RegionId, StringComparison.Ordinal))
            {
                _pendingUnlockRegionId = null;
            }

            SetFeedback(LobbyRegionMetaUtil.FormatRegionName(evt.RegionId) + " unlocked.");
        }

        private void OnRegionUnlockFailed(RegionUnlockFailed evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pendingUnlockRegionId) &&
                string.Equals(_pendingUnlockRegionId, evt.RegionId, StringComparison.Ordinal))
            {
                _pendingUnlockRegionId = null;
            }

            SetFeedback(
                LobbyRegionMetaUtil.FormatRegionName(evt.RegionId) + " unlock failed: " +
                FormatUnlockFailReason(evt.Reason));
        }

        private void HandleScene(string sceneName, bool forceRefresh)
        {
            bool shouldRun = SceneNames.IsRunSceneLike(sceneName);
            if (!shouldRun)
            {
                _isRunScene = false;
                DestroyMiniatureRig();
                return;
            }

            if (!_isRunScene || forceRefresh)
            {
                _isRunScene = true;
                _missingCityRootLogged = false;
                EnsureMiniatureRig();
                RefreshEntryVisuals();
                RefreshInfoPanel();
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

            if (_canvas == null)
            {
                CreateOverlayUi();
            }
        }

        private void DestroyMiniatureRig()
        {
            if (_canvas != null)
            {
                Object.Destroy(_canvas.gameObject);
                _canvas = null;
            }

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
            _feedbackMessage = null;
            _feedbackUntil = 0f;
            _titleText = null;
            _detailText = null;
            _missingCityRootLogged = false;
        }
        private void BuildMiniatureEntries()
        {
            _entryCount = 0;

            if (_miniatureRoot == null)
            {
                return;
            }

            GameObject cityRootObject = GameObject.Find(CityRootName);
            if (cityRootObject == null)
            {
                if (!_missingCityRootLogged)
                {
                    _missingCityRootLogged = true;
                    Debug.LogWarning("[RunRegionMiniatureManager] CityRoot not found in RunScene.");
                }

                return;
            }

            _layout = LoadLayout();
            string[] regionIds = MetaProgressionConstants.RegionIds;
            for (int i = 0; i < regionIds.Length; i++)
            {
                string regionId = regionIds[i];
                Transform source = FindSourceRegionRoot(cityRootObject.transform, regionId);
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
            }
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

        private void CreateOverlayUi()
        {
            GameObject root = new GameObject(
                UiRootName,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 3400;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RunRegionMiniatureLayoutSO.UiLayout uiLayout;
            GetUiLayout(out uiLayout);

            GameObject panel = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = uiLayout.PanelAnchoredPosition;
            panelRect.sizeDelta = uiLayout.PanelSize;

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(1f, 1f, 1f, 0.90f);

            _titleText = CreateText("Title", panelRect, 26, FontStyle.Bold, TextAnchor.UpperLeft);
            RectTransform titleRect = _titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(18f, -14f);
            titleRect.sizeDelta = new Vector2(-36f, 36f);

            _detailText = CreateText("Detail", panelRect, 18, FontStyle.Normal, TextAnchor.UpperLeft);
            RectTransform detailRect = _detailText.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 0f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.pivot = new Vector2(0f, 1f);
            detailRect.anchoredPosition = new Vector2(18f, -54f);
            detailRect.sizeDelta = new Vector2(-36f, -64f);
            _detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailText.verticalOverflow = VerticalWrapMode.Overflow;
            _detailText.lineSpacing = 1.1f;
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
                SetFeedback("This region cannot be unlocked manually.");
                return;
            }

            if (!state.CanUnlockNow)
            {
                SetFeedback("Unlock blocked: " + BuildMissingReason(state));
                return;
            }

            _pendingUnlockRegionId = regionId;
            SetFeedback(LobbyRegionMetaUtil.FormatRegionName(regionId) + " unlock requested...");
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

        private void RefreshInfoPanel()
        {
            if (_titleText == null || _detailText == null)
            {
                return;
            }

            _titleText.text = "REGION MINIATURE";

            if (_meta == null)
            {
                _detailText.text = "Meta progression service unavailable.";
                return;
            }

            string regionId = _hoveredRegionId;
            if (string.IsNullOrEmpty(regionId))
            {
                regionId = _meta.SelectedRegionId;
            }

            if (string.IsNullOrEmpty(regionId))
            {
                regionId = MetaProgressionConstants.RegionIds[0];
            }

            var builder = new StringBuilder(360);
            builder.Append(BuildRegionInfo(regionId));
            if (!string.IsNullOrEmpty(_feedbackMessage) && Time.unscaledTime < _feedbackUntil)
            {
                builder.Append("\n\n");
                builder.Append(_feedbackMessage);
            }

            _detailText.text = builder.ToString();
        }

        private string BuildRegionInfo(string regionId)
        {
            if (_meta == null)
            {
                return "Meta progression data unavailable.";
            }

            bool isUnlocked = _meta.IsRegionUnlocked(regionId);
            bool isSelected = string.Equals(_meta.SelectedRegionId, regionId, StringComparison.Ordinal);

            var builder = new StringBuilder(360);
            builder.Append("Region: ");
            builder.Append(LobbyRegionMetaUtil.FormatRegionName(regionId));
            if (isSelected)
            {
                builder.Append(" [SELECTED]");
            }

            builder.Append("\nStatus: ");
            builder.Append(isUnlocked ? "OPEN" : "LOCKED");

            if (isUnlocked)
            {
                builder.Append("\n\nUnlocked region.");
                builder.Append("\nHover another locked miniature to check unlock conditions.");
                return builder.ToString();
            }

            RegionUnlockState state;
            if (!TryBuildUnlockState(regionId, out state))
            {
                builder.Append("\n\nUnable to resolve unlock state.");
                return builder.ToString();
            }

            if (!state.HasRule)
            {
                builder.Append("\n\nStarter region. No unlock rule.");
                return builder.ToString();
            }

            builder.Append("\n\nUnlock Conditions");
            builder.Append("\n- Cost: $");
            builder.Append(state.Rule.UnlockCost);
            builder.Append("\n- Previous Region: ");
            builder.Append(LobbyRegionMetaUtil.FormatRegionName(state.Rule.PreviousRegionId));
            builder.Append(state.PreviousRegionUnlocked ? " (OPEN)" : " (LOCKED)");
            builder.Append("\n- Best Cash: $");
            builder.Append(state.BestRunCash);
            builder.Append(" / $");
            builder.Append(state.Rule.RequiredRunCash);
            builder.Append("\n- Best Rating: ");
            builder.Append(state.BestRunRating.ToString("0.0"));
            builder.Append(" / ");
            builder.Append(state.Rule.RequiredRunRating.ToString("0.0"));

            builder.Append("\n\nResult: ");
            if (state.CanUnlockNow)
            {
                builder.Append("UNLOCK AVAILABLE (click miniature).");
            }
            else
            {
                builder.Append(BuildMissingReason(state));
            }

            return builder.ToString();
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

        private static string FormatUnlockFailReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return "unknown error";
            }

            if (reason == "previous_region_locked")
            {
                return "previous region is locked";
            }

            if (reason == "requirements_not_met")
            {
                return "cash/rating requirement not met";
            }

            if (reason == "not_enough_cash")
            {
                return "not enough cash";
            }

            if (reason == "unknown_region")
            {
                return "unknown region";
            }

            return reason;
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

        private void GetUiLayout(out RunRegionMiniatureLayoutSO.UiLayout layout)
        {
            if (LoadLayout() != null)
            {
                layout = _layout.Ui;
                return;
            }

            layout = new RunRegionMiniatureLayoutSO.UiLayout
            {
                PanelAnchoredPosition = new Vector2(-18f, 18f),
                PanelSize = new Vector2(560f, 236f)
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

        private static Text CreateText(string name, Transform parent, int fontSize, FontStyle style, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.black;
            text.supportRichText = false;

            return text;
        }

        private void SetFeedback(string message)
        {
            _feedbackMessage = message;
            _feedbackUntil = Time.unscaledTime + FeedbackDuration;
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
