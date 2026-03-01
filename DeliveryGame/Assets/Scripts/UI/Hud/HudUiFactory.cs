using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal static class HudUiFactory
    {
        internal static Text CreateText(string name, Transform parent, Font font, int fontSize, TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            text.raycastTarget = false;
            return text;
        }

        internal static Button CreateButton(string name, Transform parent, Font font, Sprite panelSkinSprite, string label, Action onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.sprite = panelSkinSprite;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(0.16f, 0.2f, 0.28f, 0.95f);

            Button button = go.GetComponent<Button>();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            Text labelText = CreateText("Label", rt, font, 16, TextAnchor.MiddleCenter);
            AnchorStretch(labelText.rectTransform, 8f, 8f, 4f, 4f);
            labelText.text = label;
            return button;
        }

        internal static void AnchorStretchTop(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        internal static void AnchorStretch(RectTransform rt, float left, float right, float bottom, float top)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        internal static Image EnsureMarkerImage(RectTransform parent, string name, Color color, float size)
        {
            Transform tr = parent.Find(name);
            Image image;
            if (tr == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.sizeDelta = new Vector2(size, size);
                image = go.GetComponent<Image>();
            }
            else
            {
                image = tr.GetComponent<Image>() ?? tr.gameObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Sprite CreateCircleRingSprite(int size, float thickness)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "HudCircleRing";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float outerRadius = center - 1f;
            float innerRadius = Mathf.Max(0f, outerRadius - Mathf.Max(1f, thickness));
            float outerSq = outerRadius * outerRadius;
            float innerSq = innerRadius * innerRadius;

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
                    float distanceSq = dx * dx + dy * dy;
                    pixels[idx++] = (distanceSq <= outerSq && distanceSq >= innerSq) ? fill : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
