using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudGaugePanel
    {
        private const int FuelSegmentCount = 10;

        private RectTransform _statusGaugeRect;
        private Image _statusGaugeNeedleImage;
        private Text _statusGaugeSpeedText;
        private RectTransform _fuelGaugeRect;
        private Text _fuelGaugeText;
        private readonly Image[] _fuelSegmentImages = new Image[FuelSegmentCount];

        private float _currentSpeedKmh;
        private float _fuel01 = 1f;

        internal void BuildIfNeeded(RunHudView view, Sprite circleRingSprite)
        {
            if (view == null)
            {
                return;
            }

            RectTransform root = view.GetRootRectTransform();
            if (root == null)
            {
                return;
            }

            RectTransform statusPanel = view.GetBottomCenterPanelRectTransform();
            BuildSpeedGauge(root, statusPanel, view, circleRingSprite);
            BuildFuelGauge(root, statusPanel, view);

            UpdateSpeedVisual();
            UpdateFuelVisual();
        }

        internal void SetSpeed(float speedKmh)
        {
            _currentSpeedKmh = Mathf.Max(0f, speedKmh);
            UpdateSpeedVisual();
        }

        internal void SetFuel(float fuel01)
        {
            _fuel01 = Mathf.Clamp01(fuel01);
            UpdateFuelVisual();
        }

        internal void Cleanup()
        {
            _statusGaugeRect = null;
            _statusGaugeNeedleImage = null;
            _statusGaugeSpeedText = null;
            _fuelGaugeRect = null;
            _fuelGaugeText = null;
            for (int i = 0; i < FuelSegmentCount; i++)
            {
                _fuelSegmentImages[i] = null;
            }
        }

        private void BuildSpeedGauge(RectTransform root, RectTransform statusPanel, RunHudView view, Sprite circleRingSprite)
        {
            if (_statusGaugeRect != null)
            {
                return;
            }

            GameObject gaugeRoot = new GameObject("StatusSpeedGauge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _statusGaugeRect = gaugeRoot.GetComponent<RectTransform>();
            _statusGaugeRect.SetParent(root, false);
            _statusGaugeRect.anchorMin = new Vector2(0.5f, 0f);
            _statusGaugeRect.anchorMax = new Vector2(0.5f, 0f);
            _statusGaugeRect.pivot = new Vector2(0.5f, 0.5f);
            _statusGaugeRect.sizeDelta = new Vector2(166f, 166f);

            float gaugeX = -420f;
            float gaugeY = 82f;
            if (statusPanel != null)
            {
                gaugeX = statusPanel.anchoredPosition.x - (statusPanel.sizeDelta.x * 0.5f) - 84f;
                gaugeY = statusPanel.anchoredPosition.y + (statusPanel.sizeDelta.y * 0.5f) - 4f;
            }
            _statusGaugeRect.anchoredPosition = new Vector2(gaugeX, gaugeY);

            Image gaugeBg = gaugeRoot.GetComponent<Image>();
            gaugeBg.sprite = view.GetPanelSkinSprite();
            gaugeBg.type = gaugeBg.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            gaugeBg.color = new Color(0.07f, 0.1f, 0.14f, 0.92f);
            gaugeBg.raycastTarget = false;

            Image ring = HudUiFactory.EnsureMarkerImage(_statusGaugeRect, "GaugeRing", new Color(0.96f, 0.98f, 1f, 0.95f), 124f);
            ring.sprite = circleRingSprite != null ? circleRingSprite : HudUiFactory.CreateCircleRingSprite(128, 4f);
            ring.type = Image.Type.Simple;
            ring.rectTransform.anchoredPosition = Vector2.zero;

            GameObject needleObject = new GameObject("Needle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform needleRect = needleObject.GetComponent<RectTransform>();
            needleRect.SetParent(_statusGaugeRect, false);
            needleRect.anchorMin = new Vector2(0.5f, 0.5f);
            needleRect.anchorMax = new Vector2(0.5f, 0.5f);
            needleRect.pivot = new Vector2(0.08f, 0.5f);
            needleRect.sizeDelta = new Vector2(68f, 4f);
            needleRect.anchoredPosition = Vector2.zero;

            _statusGaugeNeedleImage = needleObject.GetComponent<Image>();
            _statusGaugeNeedleImage.color = new Color(1f, 0.76f, 0.18f, 1f);
            _statusGaugeNeedleImage.raycastTarget = false;

            Font font = view.GetDefaultFont();
            _statusGaugeSpeedText = HudUiFactory.CreateText("GaugeSpeedText", _statusGaugeRect, font, 20, TextAnchor.MiddleCenter);
            _statusGaugeSpeedText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _statusGaugeSpeedText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _statusGaugeSpeedText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _statusGaugeSpeedText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            _statusGaugeSpeedText.rectTransform.sizeDelta = new Vector2(130f, 40f);
            _statusGaugeSpeedText.color = new Color(0.96f, 0.98f, 1f, 1f);

            Text gaugeTitle = HudUiFactory.CreateText("GaugeTitle", _statusGaugeRect, font, 14, TextAnchor.MiddleCenter);
            gaugeTitle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            gaugeTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            gaugeTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            gaugeTitle.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            gaugeTitle.rectTransform.sizeDelta = new Vector2(120f, 20f);
            gaugeTitle.text = "SPEED";
            gaugeTitle.color = new Color(1f, 0.86f, 0.28f, 1f);

            Text minLabel = HudUiFactory.CreateText("GaugeMin", _statusGaugeRect, font, 12, TextAnchor.MiddleCenter);
            minLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            minLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            minLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            minLabel.rectTransform.anchoredPosition = new Vector2(-56f, -54f);
            minLabel.rectTransform.sizeDelta = new Vector2(28f, 18f);
            minLabel.text = "0";

            Text maxLabel = HudUiFactory.CreateText("GaugeMax", _statusGaugeRect, font, 12, TextAnchor.MiddleCenter);
            maxLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            maxLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            maxLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            maxLabel.rectTransform.anchoredPosition = new Vector2(56f, -54f);
            maxLabel.rectTransform.sizeDelta = new Vector2(36f, 18f);
            maxLabel.text = "180";
        }

        private void BuildFuelGauge(RectTransform root, RectTransform statusPanel, RunHudView view)
        {
            if (_fuelGaugeRect != null || statusPanel == null)
            {
                return;
            }

            GameObject gaugeObject = new GameObject("FuelGauge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _fuelGaugeRect = gaugeObject.GetComponent<RectTransform>();
            _fuelGaugeRect.SetParent(root, false);
            _fuelGaugeRect.anchorMin = new Vector2(0.5f, 0f);
            _fuelGaugeRect.anchorMax = new Vector2(0.5f, 0f);
            _fuelGaugeRect.pivot = new Vector2(0.5f, 0.5f);
            float fuelX = statusPanel.anchoredPosition.x + (statusPanel.sizeDelta.x * 0.5f) + 84f;
            float fuelY = statusPanel.anchoredPosition.y + (statusPanel.sizeDelta.y * 0.5f) - 4f;
            _fuelGaugeRect.anchoredPosition = new Vector2(fuelX, fuelY);
            _fuelGaugeRect.sizeDelta = new Vector2(178f, 84f);

            Image bg = gaugeObject.GetComponent<Image>();
            bg.sprite = view.GetPanelSkinSprite();
            bg.type = bg.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = new Color(0.07f, 0.1f, 0.14f, 0.92f);
            bg.raycastTarget = false;

            _fuelGaugeText = HudUiFactory.CreateText("FuelLabel", _fuelGaugeRect, view.GetDefaultFont(), 14, TextAnchor.MiddleCenter);
            _fuelGaugeText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _fuelGaugeText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _fuelGaugeText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _fuelGaugeText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            _fuelGaugeText.rectTransform.sizeDelta = new Vector2(146f, 20f);
            _fuelGaugeText.color = new Color(1f, 0.9f, 0.35f, 1f);
            _fuelGaugeText.text = "FUEL";

            RectTransform segmentsRoot = new GameObject("Segments", typeof(RectTransform)).GetComponent<RectTransform>();
            segmentsRoot.SetParent(_fuelGaugeRect, false);
            segmentsRoot.anchorMin = new Vector2(0f, 0f);
            segmentsRoot.anchorMax = new Vector2(1f, 1f);
            segmentsRoot.offsetMin = new Vector2(12f, 10f);
            segmentsRoot.offsetMax = new Vector2(-12f, -28f);

            for (int i = 0; i < FuelSegmentCount; i++)
            {
                Image segment = HudUiFactory.EnsureMarkerImage(segmentsRoot, "Segment_" + i, new Color(0.15f, 0.18f, 0.24f, 1f), 14f);
                RectTransform rt = segment.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(i * 15f, 0f);
                rt.sizeDelta = new Vector2(12f, 18f);
                segment.sprite = view.GetPanelSkinSprite();
                segment.type = segment.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                segment.raycastTarget = false;
                _fuelSegmentImages[i] = segment;
            }
        }

        private void UpdateSpeedVisual()
        {
            if (_statusGaugeNeedleImage == null || _statusGaugeSpeedText == null)
            {
                return;
            }

            float t = Mathf.Clamp01(_currentSpeedKmh / 180f);
            float angle = Mathf.Lerp(-120f, 120f, t);
            _statusGaugeNeedleImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            _statusGaugeSpeedText.text = Mathf.RoundToInt(_currentSpeedKmh) + "\nkm/h";
        }

        private void UpdateFuelVisual()
        {
            if (_fuelGaugeRect == null)
            {
                return;
            }

            float scaled = _fuel01 * FuelSegmentCount;
            for (int i = 0; i < FuelSegmentCount; i++)
            {
                Image segment = _fuelSegmentImages[i];
                if (segment == null)
                {
                    continue;
                }

                float fill = Mathf.Clamp01(scaled - i);
                float width = Mathf.Lerp(3f, 13f, fill);
                float height = Mathf.Lerp(12f, 20f, fill);
                segment.rectTransform.sizeDelta = new Vector2(width, height);

                if (fill <= 0.001f)
                {
                    segment.color = new Color(0.15f, 0.18f, 0.24f, 0.65f);
                }
                else if (_fuel01 <= 0.25f)
                {
                    segment.color = new Color(1f, 0.35f, 0.25f, 1f);
                }
                else if (_fuel01 <= 0.5f)
                {
                    segment.color = new Color(1f, 0.78f, 0.25f, 1f);
                }
                else
                {
                    segment.color = new Color(0.32f, 0.93f, 0.62f, 1f);
                }
            }

            if (_fuelGaugeText != null)
            {
                _fuelGaugeText.text = "FUEL  " + Mathf.RoundToInt(_fuel01 * 100f) + "%";
            }
        }
    }
}
