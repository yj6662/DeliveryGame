using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Delivery.Orders
{
    [DisallowMultipleComponent]
    internal sealed class WorldSpaceInteractPrompt : MonoBehaviour
    {
        [SerializeField] private float yOffset = 2.2f;
        [SerializeField] private Vector2 panelSize = new Vector2(2.8f, 0.72f);

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Text _text;
        private Image _panel;
        private bool _visible;

        public void SetPrompt(string message)
        {
            EnsureUi();
            if (_text != null)
            {
                _text.text = message ?? string.Empty;
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            EnsureUi();
            if (_canvas != null && _canvas.gameObject.activeSelf != visible)
            {
                _canvas.gameObject.SetActive(visible);
            }
        }

        private void Awake()
        {
            EnsureUi();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_visible || _canvasRect == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            _canvasRect.position = transform.position + (Vector3.up * yOffset);
            Vector3 forward = _canvasRect.position - cam.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                _canvasRect.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        private void EnsureUi()
        {
            if (_canvas != null && _text != null && _panel != null)
            {
                return;
            }

            Transform existing = transform.Find("WorldPromptCanvas");
            if (existing == null)
            {
                GameObject canvasObject = new GameObject("WorldPromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                _canvasRect = canvasObject.GetComponent<RectTransform>();
                _canvasRect.SetParent(transform, false);
                _canvasRect.localPosition = Vector3.up * yOffset;
                _canvasRect.localRotation = Quaternion.identity;
                _canvasRect.localScale = Vector3.one * 0.01f;

                _canvas = canvasObject.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.WorldSpace;
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 300;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10f;

                GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform panelRect = panelObject.GetComponent<RectTransform>();
                panelRect.SetParent(_canvasRect, false);
                panelRect.sizeDelta = panelSize * 100f;

                _panel = panelObject.GetComponent<Image>();
                _panel.color = new Color(0f, 0f, 0f, 0.72f);
                _panel.raycastTarget = false;

                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                RectTransform textRect = textObject.GetComponent<RectTransform>();
                textRect.SetParent(panelRect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8f, 6f);
                textRect.offsetMax = new Vector2(-8f, -6f);

                _text = textObject.GetComponent<Text>();
                _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _text.alignment = TextAnchor.MiddleCenter;
                _text.fontSize = 26;
                _text.horizontalOverflow = HorizontalWrapMode.Wrap;
                _text.verticalOverflow = VerticalWrapMode.Truncate;
                _text.color = new Color(1f, 0.97f, 0.82f, 1f);
                _text.raycastTarget = false;
                _text.text = string.Empty;
                return;
            }

            _canvasRect = existing as RectTransform;
            _canvas = existing.GetComponent<Canvas>();
            _panel = existing.GetComponentInChildren<Image>(true);
            _text = existing.GetComponentInChildren<Text>(true);
        }
    }
}
