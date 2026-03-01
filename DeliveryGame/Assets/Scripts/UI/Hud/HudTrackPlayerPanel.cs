using System.Text;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudTrackPlayerPanel
    {
        private const int ChoiceSlots = 3;

        private readonly StringBuilder _builder = new StringBuilder(128);
        private RectTransform _trackPanelRect;
        private Text _trackListText;
        private readonly Image[] _trackStackSlotImages = new Image[ChoiceSlots];

        internal void BuildIfNeeded(RunHudView view)
        {
            if (view == null || _trackPanelRect != null)
            {
                return;
            }

            RectTransform root = view.GetRootRectTransform();
            if (root == null)
            {
                return;
            }

            GameObject panelObject = new GameObject("TrackStackPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _trackPanelRect = panelObject.GetComponent<RectTransform>();
            _trackPanelRect.SetParent(root, false);
            _trackPanelRect.anchorMin = new Vector2(0.5f, 1f);
            _trackPanelRect.anchorMax = new Vector2(0.5f, 1f);
            _trackPanelRect.pivot = new Vector2(0.5f, 1f);
            _trackPanelRect.anchoredPosition = new Vector2(0f, -18f);
            _trackPanelRect.sizeDelta = new Vector2(520f, 116f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = view.GetPanelSkinSprite();
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.08f, 0.12f, 0.16f, 0.9f);
            panelImage.raycastTarget = false;

            Font font = view.GetDefaultFont();

            Text iconText = HudUiFactory.CreateText("Icon", _trackPanelRect, font, 22, TextAnchor.MiddleCenter);
            iconText.rectTransform.anchorMin = new Vector2(0f, 1f);
            iconText.rectTransform.anchorMax = new Vector2(0f, 1f);
            iconText.rectTransform.pivot = new Vector2(0f, 1f);
            iconText.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            iconText.rectTransform.sizeDelta = new Vector2(26f, 22f);
            iconText.text = "\u266B";
            iconText.color = new Color(1f, 0.84f, 0.24f, 1f);

            Text headerText = HudUiFactory.CreateText("Header", _trackPanelRect, font, 17, TextAnchor.MiddleLeft);
            headerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            headerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            headerText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            headerText.rectTransform.sizeDelta = new Vector2(-46f, 22f);
            headerText.text = "TRACK STACK";
            headerText.color = new Color(0.97f, 0.78f, 0.2f, 1f);

            _trackListText = HudUiFactory.CreateText("Tracks", _trackPanelRect, font, 16, TextAnchor.UpperLeft);
            _trackListText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _trackListText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _trackListText.rectTransform.offsetMin = new Vector2(14f, 14f);
            _trackListText.rectTransform.offsetMax = new Vector2(-14f, -32f);
            _trackListText.verticalOverflow = VerticalWrapMode.Overflow;

            for (int i = 0; i < ChoiceSlots; i++)
            {
                Image slotImage = HudUiFactory.EnsureMarkerImage(_trackPanelRect, "StackSlot_" + (i + 1), new Color(1f, 1f, 1f, 0.16f), 48f);
                RectTransform slotRect = slotImage.rectTransform;
                slotRect.anchorMin = new Vector2(1f, 1f);
                slotRect.anchorMax = new Vector2(1f, 1f);
                slotRect.pivot = new Vector2(1f, 1f);
                slotRect.anchoredPosition = new Vector2(-(14f + (i * 54f)), -12f);
                slotRect.sizeDelta = new Vector2(46f, 8f);
                slotImage.sprite = view.GetPanelSkinSprite();
                slotImage.type = slotImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                slotImage.color = new Color(1f, 1f, 1f, 0.16f);
                _trackStackSlotImages[i] = slotImage;
            }
        }

        internal void Refresh(string[] pickedTrackNames)
        {
            if (_trackListText == null || pickedTrackNames == null)
            {
                return;
            }

            _builder.Clear();
            int stackCount = 0;
            int count = Mathf.Min(ChoiceSlots, pickedTrackNames.Length);
            for (int i = 0; i < count; i++)
            {
                string trackName = pickedTrackNames[i];
                if (string.IsNullOrEmpty(trackName))
                {
                    continue;
                }

                if (stackCount > 0)
                {
                    _builder.Append('\n');
                }

                _builder.Append(i + 1).Append(". ").Append(trackName);
                stackCount++;
            }

            if (stackCount <= 0)
            {
                _trackListText.text = "-";
                for (int i = 0; i < ChoiceSlots; i++)
                {
                    Image slot = _trackStackSlotImages[i];
                    if (slot != null)
                    {
                        slot.color = new Color(1f, 1f, 1f, 0.16f);
                    }
                }

                return;
            }

            _trackListText.text = _builder.ToString();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                Image slot = _trackStackSlotImages[i];
                if (slot == null)
                {
                    continue;
                }

                slot.color = i < stackCount
                    ? new Color(1f, 0.83f, 0.2f, 0.92f)
                    : new Color(1f, 1f, 1f, 0.16f);
            }
        }

        internal void Cleanup()
        {
            _trackPanelRect = null;
            _trackListText = null;
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _trackStackSlotImages[i] = null;
            }
        }
    }
}
