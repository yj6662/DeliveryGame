using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudWorldOverlayCoordinator
    {
        private HudMinimapPanel _minimapPanel;
        private HudWorldOrderTimerPanel _worldOrderTimerPanel;
        private HudWorldRewardPopup _worldRewardPopup;
        private bool _worldRewardPopupDisabled;

        internal void BuildUiIfNeeded(RunHudView view)
        {
            EnsureMinimapPanel();
            _minimapPanel.BuildIfNeeded(view);

            EnsureWorldOrderTimerPanel();
            _worldOrderTimerPanel.BuildIfNeeded();

            if (!_worldRewardPopupDisabled)
            {
                try
                {
                    EnsureWorldRewardPopup();
                    _worldRewardPopup.BuildIfNeeded();
                }
                catch (System.Exception ex)
                {
                    _worldRewardPopupDisabled = true;
                    _worldRewardPopup?.Cleanup();
                    _worldRewardPopup = null;
                    Debug.LogException(ex);
                }
            }
        }

        internal void RefreshMinimap(ref MotorbikeController player)
        {
            _minimapPanel?.Refresh(ref player);
        }

        internal void SetObjectiveMarker(bool active, Vector3 worldPosition, OrderPointType pointType)
        {
            _minimapPanel?.SetObjectiveMarker(active, worldPosition, pointType);
        }

        internal void SetObjectiveMarkers(OrderObjectiveMarkersUpdated evt)
        {
            _minimapPanel?.SetObjectiveMarkers(evt);
        }

        internal void RefreshWorldOrderTimer(bool isRunScene, OrderFlowManager orderFlowManager, MotorbikeController player)
        {
            EnsureWorldOrderTimerPanel();
            _worldOrderTimerPanel.Refresh(isRunScene, orderFlowManager, player);
        }

        internal void UpdateWorldOrderTimerTransform(bool isRunScene, MotorbikeController player)
        {
            EnsureWorldOrderTimerPanel();
            _worldOrderTimerPanel.UpdateTransform(isRunScene, player);
        }

        internal void ShowOrderRewardPopup(int reward, float quality01, Vector3 worldPosition)
        {
            if (_worldRewardPopupDisabled)
            {
                return;
            }

            EnsureWorldRewardPopup();
            _worldRewardPopup.Show(reward, quality01, worldPosition);
        }

        internal void UpdateWorldRewardPopupTransform(bool isRunScene, MotorbikeController player)
        {
            if (_worldRewardPopupDisabled)
            {
                return;
            }

            EnsureWorldRewardPopup();
            _worldRewardPopup.UpdateTransform(isRunScene, player);
        }

        internal void ResetRuntimeState()
        {
            _minimapPanel?.ResetRuntimeState();
            _worldRewardPopup?.Hide();
        }

        internal void Cleanup()
        {
            _worldOrderTimerPanel?.Cleanup();
            _worldOrderTimerPanel = null;

            _worldRewardPopup?.Cleanup();
            _worldRewardPopup = null;
            _worldRewardPopupDisabled = false;

            _minimapPanel?.Cleanup();
            _minimapPanel = null;
        }

        private void EnsureMinimapPanel()
        {
            if (_minimapPanel == null)
            {
                _minimapPanel = new HudMinimapPanel();
            }
        }

        private void EnsureWorldOrderTimerPanel()
        {
            if (_worldOrderTimerPanel == null)
            {
                _worldOrderTimerPanel = new HudWorldOrderTimerPanel();
            }
        }

        private void EnsureWorldRewardPopup()
        {
            if (_worldRewardPopup == null)
            {
                _worldRewardPopup = new HudWorldRewardPopup();
            }
        }
    }

    internal sealed class HudWorldRewardPopup
    {
        private const string BuiltinFontPath = "LegacyRuntime.ttf";
        private const float PopupDuration = 1.35f;
        private const float PopupStartHeight = 2.4f;
        private const float PopupRiseHeight = 1.4f;
        private const float PopupBaseScaleStart = 0.0088f;
        private const float PopupBaseScaleEnd = 0.0074f;
        private const int RewardFontSize = 132;
        private const int QualityFontSize = 68;

        private GameObject _root;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _rootRect;
        private Text _rewardText;
        private Text _qualityText;
        private Camera _worldUiCamera;

        private bool _visible;
        private float _spawnTime;
        private Vector3 _spawnWorldPosition;
        private float _rewardScale = 1f;

        internal void BuildIfNeeded()
        {
            if (_root != null)
            {
                return;
            }

            Font defaultFont = ResolveBuiltinFont();

            GameObject root = new GameObject(
                "WorldRewardPopupUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            _root = root;

            _rootRect = root.GetComponent<RectTransform>();
            _rootRect.sizeDelta = new Vector2(760f, 280f);
            _rootRect.localScale = new Vector3(PopupBaseScaleStart, PopupBaseScaleStart, PopupBaseScaleStart);

            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 260;
            _worldUiCamera = Camera.main;
            _canvas.worldCamera = _worldUiCamera;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            _canvasGroup = root.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            GameObject textObject = new GameObject("RewardText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(_rootRect, false);
            textRect.anchorMin = new Vector2(0f, 0.34f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _rewardText = textObject.GetComponent<Text>();
            _rewardText.font = defaultFont;
            _rewardText.fontSize = RewardFontSize;
            _rewardText.fontStyle = FontStyle.Bold;
            _rewardText.alignment = TextAnchor.MiddleCenter;
            _rewardText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _rewardText.verticalOverflow = VerticalWrapMode.Overflow;
            _rewardText.raycastTarget = false;
            _rewardText.color = new Color(1f, 0.96f, 0.36f, 1f);

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(6f, -6f);

            GameObject qualityTextObject = new GameObject("QualityText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform qualityRect = qualityTextObject.GetComponent<RectTransform>();
            qualityRect.SetParent(_rootRect, false);
            qualityRect.anchorMin = new Vector2(0f, 0f);
            qualityRect.anchorMax = new Vector2(1f, 0.4f);
            qualityRect.offsetMin = Vector2.zero;
            qualityRect.offsetMax = Vector2.zero;

            _qualityText = qualityTextObject.GetComponent<Text>();
            _qualityText.font = defaultFont;
            _qualityText.fontSize = QualityFontSize;
            _qualityText.fontStyle = FontStyle.Bold;
            _qualityText.alignment = TextAnchor.MiddleCenter;
            _qualityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _qualityText.verticalOverflow = VerticalWrapMode.Overflow;
            _qualityText.raycastTarget = false;
            _qualityText.color = new Color(0.35f, 0.72f, 1f, 1f);

            Outline qualityOutline = qualityTextObject.GetComponent<Outline>();
            qualityOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            qualityOutline.effectDistance = new Vector2(3f, -3f);

            root.SetActive(false);
        }

        internal void Show(int reward, float quality01, Vector3 worldPosition)
        {
            if (reward <= 0)
            {
                return;
            }

            BuildIfNeeded();
            if (_root == null)
            {
                return;
            }

            _visible = true;
            _spawnTime = Time.unscaledTime;
            _spawnWorldPosition = worldPosition + (Vector3.up * PopupStartHeight);
            _rewardScale = ComputeRewardScale(reward);
            ResolveQualityStyle(quality01, out string qualityLabel, out Color qualityColor);

            if (_rewardText != null)
            {
                _rewardText.text = "+$" + reward;
                _rewardText.fontSize = Mathf.RoundToInt(RewardFontSize * Mathf.Lerp(0.96f, 1.26f, Mathf.Clamp01((_rewardScale - 1f) / 0.75f)));
            }

            if (_qualityText != null)
            {
                _qualityText.text = qualityLabel + "  " + Mathf.RoundToInt(Mathf.Clamp01(quality01) * 100f) + "%";
                _qualityText.color = qualityColor;
                _qualityText.fontSize = QualityFontSize;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }

            UpdateTransform(true, null);
        }

        internal void UpdateTransform(bool isRunScene, MotorbikeController _unusedPlayer)
        {
            if (!isRunScene || !_visible || _root == null)
            {
                Hide();
                return;
            }

            float elapsed = Time.unscaledTime - _spawnTime;
            float t = PopupDuration > 0.0001f ? Mathf.Clamp01(elapsed / PopupDuration) : 1f;
            if (t >= 1f)
            {
                Hide();
                return;
            }

            if (_worldUiCamera == null || !_worldUiCamera.isActiveAndEnabled)
            {
                _worldUiCamera = Camera.main;
            }

            if (_canvas != null)
            {
                _canvas.worldCamera = _worldUiCamera;
            }

            float eased = 1f - ((1f - t) * (1f - t));
            Vector3 worldPosition = _spawnWorldPosition + (Vector3.up * (PopupRiseHeight * eased));
            _root.transform.position = worldPosition;

            float scale = Mathf.Lerp(PopupBaseScaleStart, PopupBaseScaleEnd, t) * _rewardScale;
            _rootRect.localScale = new Vector3(scale, scale, scale);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            }

            if (_worldUiCamera == null)
            {
                return;
            }

            Vector3 lookDirection = _worldUiCamera.transform.forward;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                lookDirection = _worldUiCamera.transform.forward;
            }

            _root.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        internal void Hide()
        {
            _visible = false;
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        internal void Cleanup()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            _root = null;
            _rootRect = null;
            _canvas = null;
            _canvasGroup = null;
            _rewardText = null;
            _qualityText = null;
            _worldUiCamera = null;
            _visible = false;
            _spawnTime = 0f;
            _spawnWorldPosition = Vector3.zero;
            _rewardScale = 1f;
        }

        private static Font ResolveBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>(BuiltinFontPath);
            if (font == null)
            {
                Debug.LogWarning("[HudWorldRewardPopup] Built-in font not found: " + BuiltinFontPath);
            }

            return font;
        }

        private static float ComputeRewardScale(int reward)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(80f, 650f, reward));
            return Mathf.Lerp(1f, 1.75f, t);
        }

        private static void ResolveQualityStyle(float quality01, out string label, out Color color)
        {
            float q = Mathf.Clamp01(quality01);
            if (q >= 0.9f)
            {
                label = "PERFECT";
                color = new Color(0.35f, 0.72f, 1f, 1f);
                return;
            }

            if (q >= 0.6f)
            {
                label = "GOOD";
                color = new Color(0.36f, 1f, 0.42f, 1f);
                return;
            }

            if (q >= 0.3f)
            {
                label = "MESSY";
                color = new Color(1f, 0.84f, 0.28f, 1f);
                return;
            }

            label = "RUINED";
            color = new Color(1f, 0.36f, 0.36f, 1f);
        }
    }
}
