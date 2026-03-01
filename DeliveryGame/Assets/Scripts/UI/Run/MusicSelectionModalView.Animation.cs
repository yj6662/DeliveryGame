using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed partial class MusicSelectionModalView
    {
        private void UpdateCardScaleAnimation(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            float lerpT = 1f - Mathf.Exp(-ScaleLerpSpeed * dt);
            for (int i = 0; i < 3; i++)
            {
                RectTransform card = _optionCardRects[i];
                if (card == null)
                {
                    continue;
                }

                float next = Mathf.Lerp(card.localScale.x, _targetScales[i], lerpT);
                card.localScale = new Vector3(next, next, 1f);
            }
        }

        private void UpdateContentAnimation(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            float posLerp = 1f - Mathf.Exp(-TextMoveLerpSpeed * dt);
            float alphaLerp = 1f - Mathf.Exp(-DetailFadeSpeed * dt);

            for (int i = 0; i < 3; i++)
            {
                _titleCurrentY[i] = Mathf.Lerp(_titleCurrentY[i], _targetTitleY[i], posLerp);
                _subtitleCurrentY[i] = Mathf.Lerp(_subtitleCurrentY[i], _targetSubtitleY[i], posLerp);
                _detailCurrentAlpha[i] = Mathf.Lerp(_detailCurrentAlpha[i], _targetDetailAlpha[i], alphaLerp);

                ApplyTextPositions(i);
                ApplyDetailAlpha(i, _detailCurrentAlpha[i]);
            }
        }

        private void ApplyTextPositions(int index)
        {
            RectTransform titleRect = _titleRects[index];
            if (titleRect != null)
            {
                Vector2 p = titleRect.anchoredPosition;
                p.y = _titleCurrentY[index];
                titleRect.anchoredPosition = p;
            }

            RectTransform subRect = _subtitleRects[index];
            if (subRect != null)
            {
                Vector2 p = subRect.anchoredPosition;
                p.y = _subtitleCurrentY[index];
                subRect.anchoredPosition = p;
            }
        }

        private void ApplyDetailAlpha(int index, float alpha)
        {
            if (optionSynergyTexts == null || index >= optionSynergyTexts.Length)
            {
                return;
            }

            Text detail = optionSynergyTexts[index];
            if (detail == null)
            {
                return;
            }

            Color c = detail.color;
            c.a = Mathf.Clamp01(alpha);
            detail.color = c;
        }

        private void UpdateCardThemeAnimation(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            float lerpT = 1f - Mathf.Exp(-TierColorLerpSpeed * dt);
            for (int i = 0; i < 3; i++)
            {
                ApplyCardTheme(i, lerpT, false);
            }
        }

        private void ApplyCardThemeImmediate(int index)
        {
            ApplyCardTheme(index, 1f, true);
        }

        private void ApplyCardTheme(int index, float lerpT, bool immediate)
        {
            Image card = _cardImages[index];
            Image header = _headerImages[index];
            Image glow = _glowBorders[index];
            bool selected = index == _selectedIndex;

            Color baseCard;
            Color baseHeader;
            Color border;
            GetTierPalette(_tierCodes[index], out baseCard, out baseHeader, out border);

            Color targetCard = selected ? Color.Lerp(baseCard, Color.white, 0.10f) : baseCard;
            Color targetHeader = selected ? Color.Lerp(baseHeader, Color.white, 0.06f) : baseHeader;

            if (card != null)
            {
                card.color = immediate ? targetCard : Color.Lerp(card.color, targetCard, lerpT);
            }

            if (header != null)
            {
                header.color = immediate ? targetHeader : Color.Lerp(header.color, targetHeader, lerpT);
            }

            if (glow != null)
            {
                float targetAlpha;
                if (_immediateSynergy[index])
                {
                    float pulse = (Mathf.Sin(Time.unscaledTime * GlowPulseSpeed + index) + 1f) * 0.5f;
                    targetAlpha = selected
                        ? Mathf.Lerp(0.45f, 0.92f, pulse)
                        : Mathf.Lerp(0.28f, 0.72f, pulse);
                }
                else
                {
                    targetAlpha = selected ? 0.18f : 0.06f;
                }

                Color targetGlow = border;
                targetGlow.a = targetAlpha;
                glow.color = immediate ? targetGlow : Color.Lerp(glow.color, targetGlow, lerpT);
            }
        }

        private static void GetTierPalette(int tierCode, out Color card, out Color header, out Color border)
        {
            switch (tierCode)
            {
                case 2: // Epic
                    card = new Color(0.99f, 0.9f, 0.83f, 0.96f);
                    header = new Color(0.48f, 0.2f, 0.07f, 0.94f);
                    border = new Color(1f, 0.68f, 0.22f, 1f);
                    break;
                case 1: // Rare
                    card = new Color(0.91f, 0.9f, 0.99f, 0.96f);
                    header = new Color(0.19f, 0.1f, 0.39f, 0.94f);
                    border = new Color(0.67f, 0.56f, 1f, 1f);
                    break;
                default: // Common
                    card = new Color(0.9f, 0.96f, 0.99f, 0.96f);
                    header = new Color(0.07f, 0.24f, 0.33f, 0.94f);
                    border = new Color(0.45f, 0.82f, 1f, 1f);
                    break;
            }
        }
    }
}
