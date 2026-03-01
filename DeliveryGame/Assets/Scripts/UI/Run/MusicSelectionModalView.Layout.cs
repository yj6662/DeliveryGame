using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed partial class MusicSelectionModalView
    {
        private void EnsureSiblingOrder()
        {
            Transform dim = transform.Find("DimBackground");
            if (dim != null)
            {
                dim.SetAsFirstSibling();
            }
        }

        private void ConfigureTitleTextLayout()
        {
            if (optionTitleTexts == null)
            {
                return;
            }

            for (int i = 0; i < optionTitleTexts.Length; i++)
            {
                Text text = optionTitleTexts[i];
                if (text == null)
                {
                    continue;
                }

                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void CacheCardRects()
        {
            for (int i = 0; i < 3; i++)
            {
                _optionCardRects[i] = null;
                _titleRects[i] = null;
                _subtitleRects[i] = null;
                _cardImages[i] = null;
                _headerImages[i] = null;
                _glowBorders[i] = null;
                _targetScales[i] = 1f;
                _titleCurrentY[i] = UnselectedTitleY;
                _subtitleCurrentY[i] = UnselectedSubY;
                _targetTitleY[i] = UnselectedTitleY;
                _targetSubtitleY[i] = UnselectedSubY;
                _detailCurrentAlpha[i] = 0f;
                _targetDetailAlpha[i] = 0f;
            }

            int buttonCount = optionButtons != null ? optionButtons.Length : 0;
            for (int i = 0; i < 3 && i < buttonCount; i++)
            {
                Button button = optionButtons[i];
                if (button == null)
                {
                    continue;
                }

                RectTransform cardRect = button.transform.parent as RectTransform;
                if (cardRect == null)
                {
                    continue;
                }

                _optionCardRects[i] = cardRect;
                _cardImages[i] = cardRect.GetComponent<Image>();

                Transform headerStrip = cardRect.Find("HeaderStrip");
                _headerImages[i] = headerStrip != null ? headerStrip.GetComponent<Image>() : null;
                _glowBorders[i] = EnsureGlowBorder(cardRect, _cardImages[i]);

                Transform centerPanel = cardRect.Find("ActivatablePanel");
                if (centerPanel != null)
                {
                    centerPanel.gameObject.SetActive(false);
                }

                _titleRects[i] = optionTitleTexts != null && i < optionTitleTexts.Length && optionTitleTexts[i] != null
                    ? optionTitleTexts[i].rectTransform
                    : null;
                if (_titleRects[i] != null)
                {
                    _titleRects[i].anchorMin = new Vector2(0f, 0.5f);
                    _titleRects[i].anchorMax = new Vector2(1f, 0.5f);
                    _titleRects[i].pivot = new Vector2(0.5f, 0.5f);
                    _titleRects[i].sizeDelta = new Vector2(-36f, 72f);
                    _titleRects[i].anchoredPosition = new Vector2(0f, UnselectedTitleY);
                }

                _subtitleRects[i] = optionSubTexts != null && i < optionSubTexts.Length && optionSubTexts[i] != null
                    ? optionSubTexts[i].rectTransform
                    : null;
                if (_subtitleRects[i] != null)
                {
                    _subtitleRects[i].anchorMin = new Vector2(0f, 0.5f);
                    _subtitleRects[i].anchorMax = new Vector2(1f, 0.5f);
                    _subtitleRects[i].pivot = new Vector2(0.5f, 0.5f);
                    _subtitleRects[i].sizeDelta = new Vector2(-42f, 46f);
                    _subtitleRects[i].anchoredPosition = new Vector2(0f, UnselectedSubY);
                    optionSubTexts[i].alignment = TextAnchor.MiddleCenter;
                    optionSubTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                    optionSubTexts[i].verticalOverflow = VerticalWrapMode.Truncate;
                }

                if (optionSynergyTexts != null && i < optionSynergyTexts.Length && optionSynergyTexts[i] != null)
                {
                    RectTransform detailRect = optionSynergyTexts[i].rectTransform;
                    detailRect.anchorMin = new Vector2(0f, 0.5f);
                    detailRect.anchorMax = new Vector2(1f, 0.5f);
                    detailRect.pivot = new Vector2(0.5f, 0.5f);
                    detailRect.sizeDelta = new Vector2(-40f, 210f);
                    detailRect.anchoredPosition = new Vector2(0f, DetailTextY);
                    optionSynergyTexts[i].alignment = TextAnchor.UpperCenter;
                    optionSynergyTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                    optionSynergyTexts[i].verticalOverflow = VerticalWrapMode.Overflow;
                    ApplyDetailAlpha(i, 0f);
                }

                cardRect.localScale = Vector3.one;
                ApplyCardThemeImmediate(i);
            }
        }

        private static Image EnsureGlowBorder(RectTransform cardRect, Image cardImage)
        {
            Transform existing = cardRect.Find("SynergyGlowBorder");
            Image glow;
            if (existing == null)
            {
                GameObject go = new GameObject("SynergyGlowBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(cardRect, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-4f, -4f);
                rt.offsetMax = new Vector2(4f, 4f);
                glow = go.GetComponent<Image>();
            }
            else
            {
                glow = existing.GetComponent<Image>();
                if (glow == null)
                {
                    glow = existing.gameObject.AddComponent<Image>();
                }
            }

            if (cardImage != null)
            {
                glow.sprite = cardImage.sprite;
                glow.type = cardImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            }

            glow.raycastTarget = false;
            glow.color = new Color(1f, 1f, 1f, 0f);
            return glow;
        }

        private void EnsureCanvasRaycaster()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static string NormalizeTitleSingleLine(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            string title = raw.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (title.Length <= 0)
            {
                return string.Empty;
            }

            int end = title.Length - 1;
            while (end >= 0 && char.IsDigit(title[end]))
            {
                end--;
            }

            if (end < title.Length - 1)
            {
                while (end >= 0 && char.IsWhiteSpace(title[end]))
                {
                    end--;
                }

                if (end >= 0 && (title[end] == '-' || title[end] == '_' || title[end] == ':' || title[end] == '#'))
                {
                    end--;
                    while (end >= 0 && char.IsWhiteSpace(title[end]))
                    {
                        end--;
                    }
                }

                if (end >= 0)
                {
                    title = title.Substring(0, end + 1).TrimEnd();
                }
            }

            return title;
        }
    }
}
