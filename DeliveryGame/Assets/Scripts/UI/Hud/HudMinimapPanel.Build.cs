using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class HudMinimapPanel
    {
        internal void BuildIfNeeded(RunHudView view)
        {
            if (view == null || _minimapMaskRect != null)
            {
                return;
            }

            RectTransform panelRect = view.GetMinimapPanelRectTransform();
            Image maskImage = view.GetMinimapImage();
            if (panelRect == null || maskImage == null)
            {
                return;
            }

            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(MinimapPanelOffset, MinimapPanelOffset);
            panelRect.sizeDelta = new Vector2(MinimapPanelSize, MinimapPanelSize);

            Image panelImage = panelRect.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.08f, 0.1f, 0.13f, 0.88f);
                panelImage.raycastTarget = false;
            }

            if (_circleMaskSprite == null)
            {
                _circleMaskSprite = CreateCircleMaskSprite(256);
            }

            Mask mask = maskImage.GetComponent<Mask>();
            if (mask == null)
            {
                mask = maskImage.gameObject.AddComponent<Mask>();
            }

            mask.showMaskGraphic = true;
            maskImage.sprite = _circleMaskSprite;
            maskImage.type = Image.Type.Simple;
            maskImage.color = new Color(0.03f, 0.03f, 0.03f, 0.98f);
            maskImage.raycastTarget = false;

            _minimapMaskRect = maskImage.rectTransform;
            _minimapMaskRect.anchorMin = new Vector2(0f, 0f);
            _minimapMaskRect.anchorMax = new Vector2(1f, 1f);
            _minimapMaskRect.offsetMin = new Vector2(MinimapInnerPadding, MinimapInnerPadding);
            _minimapMaskRect.offsetMax = new Vector2(-MinimapInnerPadding, -MinimapInnerPadding);

            Transform mapRender = _minimapMaskRect.Find("MapRender");
            if (mapRender == null)
            {
                GameObject mapRenderObject = new GameObject("MapRender", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform mapRenderRect = mapRenderObject.GetComponent<RectTransform>();
                mapRenderRect.SetParent(_minimapMaskRect, false);
                mapRenderRect.anchorMin = Vector2.zero;
                mapRenderRect.anchorMax = Vector2.one;
                mapRenderRect.offsetMin = Vector2.zero;
                mapRenderRect.offsetMax = Vector2.zero;
                _minimapRawImage = mapRenderObject.GetComponent<RawImage>();
            }
            else
            {
                _minimapRawImage = mapRender.GetComponent<RawImage>();
                if (_minimapRawImage == null)
                {
                    _minimapRawImage = mapRender.gameObject.AddComponent<RawImage>();
                }
            }

            _minimapRawImage.raycastTarget = false;

            EnsureMinimapCamera();
            _minimapRawImage.texture = _minimapRt;
            _minimapRawImage.color = Color.white;

            RectTransform overlay = _minimapMaskRect.Find("Overlay") as RectTransform;
            if (overlay == null)
            {
                GameObject overlayObject = new GameObject("Overlay", typeof(RectTransform));
                overlay = overlayObject.GetComponent<RectTransform>();
                overlay.SetParent(_minimapMaskRect, false);
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = Vector2.zero;
                overlay.offsetMax = Vector2.zero;
            }

            Font defaultFont = view.GetDefaultFont();
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                Color markerColor = GetMarkerColorForType(_objectiveMarkerPointTypes[i]);
                float markerSize = i == 0 ? 16f : 13f;

                Image dot = HudUiFactory.EnsureMarkerImage(overlay, "ObjectiveDot_" + i, markerColor, markerSize);
                dot.gameObject.SetActive(false);
                _objectiveDots[i] = dot;
                _objectiveDotRects[i] = dot.rectTransform;

                Image edgeBadge = HudUiFactory.EnsureMarkerImage(overlay, "ObjectiveEdgeBadge_" + i, markerColor, i == 0 ? 18f : 16f);
                edgeBadge.sprite = _circleMaskSprite;
                edgeBadge.type = Image.Type.Simple;
                edgeBadge.gameObject.SetActive(false);
                _objectiveEdgeBadges[i] = edgeBadge;
                _objectiveEdgeRects[i] = edgeBadge.rectTransform;

                Text edgeText = EnsureMarkerText(edgeBadge.rectTransform, defaultFont, "Arrow", ">", new Color(0f, 0f, 0f, 1f), 14);
                edgeText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.sizeDelta = new Vector2(18f, 18f);
                edgeText.rectTransform.anchoredPosition = Vector2.zero;
                edgeText.gameObject.SetActive(false);
                _objectiveEdgeTexts[i] = edgeText;

                ApplyMarkerColor(i);
            }

            Text playerArrow = EnsureMarkerText(overlay, defaultFont, "PlayerArrow", "^", new Color(0.25f, 1f, 0.8f, 1f), 24);
            playerArrow.rectTransform.anchoredPosition = Vector2.zero;
            playerArrow.rectTransform.localRotation = Quaternion.identity;
        }

        private static Sprite CreateCircleMaskSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "MinimapCircleMask";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            float radiusSq = radius * radius;
            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 fill = new Color32(255, 255, 255, 255);

            Color32[] pixels = new Color32[size * size];
            int idx = 0;
            for (int y = 0; y < size; y++)
            {
                float dy = y - center;
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    pixels[idx++] = (dx * dx + dy * dy) <= radiusSq ? fill : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Text EnsureMarkerText(RectTransform parent, Font font, string name, string value, Color color, int fontSize)
        {
            Transform markerTransform = parent.Find(name);
            Text text;
            if (markerTransform == null)
            {
                GameObject markerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                RectTransform markerRect = markerObject.GetComponent<RectTransform>();
                markerRect.SetParent(parent, false);
                markerRect.sizeDelta = new Vector2(28f, 28f);
                text = markerObject.GetComponent<Text>();
            }
            else
            {
                text = markerTransform.GetComponent<Text>() ?? markerTransform.gameObject.AddComponent<Text>();
            }

            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }
}
