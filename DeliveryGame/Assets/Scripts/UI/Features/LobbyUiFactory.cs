using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Features
{
    internal static class LobbyUiFactory
    {
        internal static Text CreateText(
            string name,
            Transform parent,
            Font font,
            int size,
            TextAnchor anchor,
            Color color)
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

        internal static Button CreateButton(string name, Transform parent, Font font, string label)
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

        internal static Slider CreateSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
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
    }
}
