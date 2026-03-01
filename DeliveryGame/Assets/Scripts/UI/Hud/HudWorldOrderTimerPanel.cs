using DeliveryRun.Delivery.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudWorldOrderTimerPanel
    {
        private const int MaxWorldOrderTimerSlots = 3;

        private GameObject _worldOrderTimerRoot;
        private Canvas _worldOrderTimerCanvas;
        private readonly RectTransform[] _worldOrderTimerRows = new RectTransform[MaxWorldOrderTimerSlots];
        private readonly Image[] _worldOrderTimerRowImages = new Image[MaxWorldOrderTimerSlots];
        private readonly Image[] _worldOrderTimerFillImages = new Image[MaxWorldOrderTimerSlots];
        private readonly ActiveOrderTimerView[] _worldOrderTimerViews =
            new ActiveOrderTimerView[MaxWorldOrderTimerSlots];
        private Camera _worldUiCamera;

        internal void BuildIfNeeded()
        {
            if (_worldOrderTimerRoot != null)
            {
                return;
            }

            GameObject root = new GameObject(
                "WorldOrderTimerUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _worldOrderTimerRoot = root;

            _worldOrderTimerCanvas = root.GetComponent<Canvas>();
            _worldOrderTimerCanvas.renderMode = RenderMode.WorldSpace;
            _worldOrderTimerCanvas.overrideSorting = true;
            _worldOrderTimerCanvas.sortingOrder = 240;
            _worldUiCamera = Camera.main;
            _worldOrderTimerCanvas.worldCamera = _worldUiCamera;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(300f, 126f);
            rootRect.localScale = new Vector3(0.003f, 0.003f, 0.003f);

            for (int i = 0; i < MaxWorldOrderTimerSlots; i++)
            {
                GameObject row = new GameObject("Row_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.SetParent(rootRect, false);
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.sizeDelta = new Vector2(0f, 30f);
                rowRect.anchoredPosition = new Vector2(0f, -6f - (i * 38f));

                Image rowBg = row.GetComponent<Image>();
                rowBg.color = new Color(0f, 0f, 0f, 0.62f);

                GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform fillRect = fillObject.GetComponent<RectTransform>();
                fillRect.SetParent(rowRect, false);
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                Image fillImage = fillObject.GetComponent<Image>();
                fillImage.color = new Color(0.36f, 1f, 0.42f, 0.95f);
                fillImage.raycastTarget = false;

                _worldOrderTimerRows[i] = rowRect;
                _worldOrderTimerRowImages[i] = rowBg;
                _worldOrderTimerFillImages[i] = fillImage;
                row.SetActive(false);
            }

            root.SetActive(false);
        }

        internal void Refresh(bool isRunScene, OrderFlowManager orderFlowManager, MotorbikeController player)
        {
            if (!isRunScene || player == null || orderFlowManager == null)
            {
                Hide();
                return;
            }

            BuildIfNeeded();
            if (_worldOrderTimerRoot == null)
            {
                return;
            }

            int count = orderFlowManager.CopyActiveOrderTimerViewsNonAlloc(_worldOrderTimerViews);
            if (count <= 0)
            {
                Hide();
                return;
            }

            _worldOrderTimerRoot.SetActive(true);
            UpdateTransform(isRunScene, player);

            int visible = count < MaxWorldOrderTimerSlots ? count : MaxWorldOrderTimerSlots;
            for (int i = 0; i < MaxWorldOrderTimerSlots; i++)
            {
                RectTransform row = _worldOrderTimerRows[i];
                Image bg = _worldOrderTimerRowImages[i];
                Image fill = _worldOrderTimerFillImages[i];
                if (row == null || bg == null || fill == null)
                {
                    continue;
                }

                if (i >= visible)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                ActiveOrderTimerView info = _worldOrderTimerViews[i];
                float remaining = Mathf.Max(0f, info.RemainingSeconds);
                float limit = Mathf.Max(1f, info.LimitSeconds);
                float ratio = remaining / limit;
                Color tint = GetOrderTimerTint(ratio);
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMax = new Vector2(ratio, 1f);
                fill.color = tint;
                bg.color = new Color(0f, 0f, 0f, 0.62f);
                row.gameObject.SetActive(true);
            }
        }

        internal void UpdateTransform(bool isRunScene, MotorbikeController player)
        {
            if (!isRunScene || _worldOrderTimerRoot == null || !_worldOrderTimerRoot.activeSelf || player == null)
            {
                return;
            }

            _worldOrderTimerRoot.transform.position = player.transform.position + new Vector3(0f, 2.35f, 0f);

            if (_worldUiCamera == null || !_worldUiCamera.isActiveAndEnabled)
            {
                _worldUiCamera = Camera.main;
            }

            if (_worldOrderTimerCanvas != null)
            {
                _worldOrderTimerCanvas.worldCamera = _worldUiCamera;
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

            _worldOrderTimerRoot.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        internal void Hide()
        {
            if (_worldOrderTimerRoot != null)
            {
                _worldOrderTimerRoot.SetActive(false);
            }
        }

        internal void Cleanup()
        {
            if (_worldOrderTimerRoot != null)
            {
                Object.Destroy(_worldOrderTimerRoot);
            }

            _worldOrderTimerRoot = null;
            _worldOrderTimerCanvas = null;
            _worldUiCamera = null;
            for (int i = 0; i < MaxWorldOrderTimerSlots; i++)
            {
                _worldOrderTimerRows[i] = null;
                _worldOrderTimerRowImages[i] = null;
                _worldOrderTimerFillImages[i] = null;
                _worldOrderTimerViews[i] = default;
            }
        }

        private static Color GetOrderTimerTint(float ratio)
        {
            if (ratio > 0.5f)
            {
                return new Color(0.36f, 1f, 0.42f, 1f);
            }

            if (ratio > 0.25f)
            {
                return new Color(1f, 0.88f, 0.28f, 1f);
            }

            return new Color(1f, 0.36f, 0.36f, 1f);
        }
    }
}
