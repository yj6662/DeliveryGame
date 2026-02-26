using System;
using DeliveryRun.Delivery.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    [DisallowMultipleComponent]
    internal sealed class OptionHoverRelay : MonoBehaviour, IPointerEnterHandler
    {
        public int Index;
        public Action<int> OnHover;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Action<int> callback = OnHover;
            if (callback != null)
            {
                callback(Index);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class OptionClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public int Index;
        public Action<int> OnClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            Action<int> callback = OnClick;
            if (callback != null)
            {
                callback(Index);
            }
        }
    }

    public sealed class MusicSelectionModalView : MonoBehaviour
    {
        private const float SelectedCardScale = 1.06f;
        private const float UnselectedCardScale = 0.95f;
        private const float ScaleLerpSpeed = 14f;
        private const float TextMoveLerpSpeed = 12f;
        private const float DetailFadeSpeed = 10f;
        private const float TierColorLerpSpeed = 10f;
        private const float GlowPulseSpeed = 3.2f;
        private const float HoverResumePointerMovePixels = 4f;

        private const float UnselectedTitleY = 36f;
        private const float UnselectedSubY = -6f;
        private const float SelectedTitleY = 170f;
        private const float SelectedSubY = 132f;
        private const float DetailTextY = -24f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Text[] optionTitleTexts;
        [SerializeField] private Text[] optionSubTexts;
        [SerializeField] private Text[] optionSynergyTexts;
        [SerializeField] private Button closeButton;

        private Action<int> _onSelect;

        private readonly string[] _optionDetailTexts = new string[3];
        private readonly string[] _optionSubDetailTexts = new string[3];
        private readonly RectTransform[] _optionCardRects = new RectTransform[3];
        private readonly RectTransform[] _titleRects = new RectTransform[3];
        private readonly RectTransform[] _subtitleRects = new RectTransform[3];
        private readonly Image[] _cardImages = new Image[3];
        private readonly Image[] _headerImages = new Image[3];
        private readonly Image[] _glowBorders = new Image[3];
        private readonly float[] _targetScales = new float[3];
        private readonly float[] _titleCurrentY = new float[3];
        private readonly float[] _subtitleCurrentY = new float[3];
        private readonly float[] _targetTitleY = new float[3];
        private readonly float[] _targetSubtitleY = new float[3];
        private readonly float[] _detailCurrentAlpha = new float[3];
        private readonly float[] _targetDetailAlpha = new float[3];
        private readonly int[] _tierCodes = new int[3];
        private readonly bool[] _immediateSynergy = new bool[3];

        private int _selectedIndex = -1;
        private bool _shown;
        private bool _suppressHoverUntilPointerMove;
        private Vector2 _pointerAtKeySelect;

        public bool IsReadyForSelectionForTests
        {
            get { return _shown && _onSelect != null; }
        }

        private void Awake()
        {
            EnsureCanvasRaycaster();
            EnsureSiblingOrder();
            ConfigureTitleTextLayout();
            CacheCardRects();
            Hide();
        }

        public void Show(Action<int> onSelect)
        {
            _onSelect = onSelect;
            EnsureCanvasRaycaster();
            EnsureSiblingOrder();
            ConfigureTitleTextLayout();
            CacheCardRects();
            BindInteractiveRelays();
            SetSelectedIndex(0, true);

            _suppressHoverUntilPointerMove = false;
            _pointerAtKeySelect = Vector2.zero;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            _shown = true;
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _shown = false;
        }

        public void SetOption(int i, string title, string sub, string detail)
        {
            if (i < 0 || i >= 3)
            {
                return;
            }

            _optionSubDetailTexts[i] = sub ?? string.Empty;
            _optionDetailTexts[i] = detail ?? string.Empty;

            if (optionTitleTexts != null && i < optionTitleTexts.Length && optionTitleTexts[i] != null)
            {
                optionTitleTexts[i].text = NormalizeTitleSingleLine(title);
            }

            if (optionSubTexts != null && i < optionSubTexts.Length && optionSubTexts[i] != null)
            {
                optionSubTexts[i].text = _optionSubDetailTexts[i];
            }

            if (optionSynergyTexts != null && i < optionSynergyTexts.Length && optionSynergyTexts[i] != null)
            {
                optionSynergyTexts[i].text = _optionDetailTexts[i];
            }

            ApplyDetailAlpha(i, _detailCurrentAlpha[i]);
        }

        public void SetOptionVisual(int i, int tierCode, bool immediateSynergy)
        {
            if (i < 0 || i >= 3)
            {
                return;
            }

            _tierCodes[i] = tierCode;
            _immediateSynergy[i] = immediateSynergy;
            ApplyCardThemeImmediate(i);
        }

        public bool ConfirmSelectionForTests(int index)
        {
            if (!_shown || _onSelect == null)
            {
                return false;
            }

            SetSelectedIndex(index, false);
            ConfirmSelection(index);
            return true;
        }

        private void Update()
        {
            if (!_shown)
            {
                return;
            }

            int keyIndex = RuntimeInput.ReadMusicOptionIndexPressedThisFrame();
            if (keyIndex >= 0)
            {
                SetSelectedIndex(keyIndex, false);
                _suppressHoverUntilPointerMove = true;
                if (!RuntimeInput.TryReadPointerScreenPosition(out _pointerAtKeySelect))
                {
                    _pointerAtKeySelect = Vector2.negativeInfinity;
                }
            }

            TryUpdateHoverFromPointer();

            float dt = Time.unscaledDeltaTime;
            UpdateCardScaleAnimation(dt);
            UpdateContentAnimation(dt);
            UpdateCardThemeAnimation(dt);

            if (RuntimeInput.WasSubmitPressedThisFrame())
            {
                ConfirmSelection(_selectedIndex >= 0 ? _selectedIndex : 0);
            }
        }

        private void BindInteractiveRelays()
        {
            for (int i = 0; i < 3; i++)
            {
                RectTransform cardRect = _optionCardRects[i];
                if (cardRect == null)
                {
                    continue;
                }

                Image cardImage = _cardImages[i];
                if (cardImage != null)
                {
                    cardImage.raycastTarget = true;
                }

                OptionHoverRelay hoverRelay = cardRect.GetComponent<OptionHoverRelay>();
                if (hoverRelay == null)
                {
                    hoverRelay = cardRect.gameObject.AddComponent<OptionHoverRelay>();
                }
                hoverRelay.Index = i;
                hoverRelay.OnHover = OnHovered;

                OptionClickRelay clickRelay = cardRect.GetComponent<OptionClickRelay>();
                if (clickRelay == null)
                {
                    clickRelay = cardRect.gameObject.AddComponent<OptionClickRelay>();
                }
                clickRelay.Index = i;
                clickRelay.OnClick = OnCardClicked;

                if (optionButtons != null && i < optionButtons.Length && optionButtons[i] != null)
                {
                    // Legacy bottom Select button is hidden; card click is the only confirm interaction.
                    optionButtons[i].gameObject.SetActive(false);
                }
            }

            if (closeButton != null)
            {
                closeButton.interactable = true;
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void OnHovered(int index)
        {
            if (!_shown)
            {
                return;
            }

            _suppressHoverUntilPointerMove = false;
            SetSelectedIndex(index, false);
        }

        private void OnCardClicked(int index)
        {
            if (!_shown)
            {
                return;
            }

            _suppressHoverUntilPointerMove = false;
            SetSelectedIndex(index, false);
            ConfirmSelection(index);
        }

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
                _headerImages[i] = cardRect.Find("HeaderStrip") != null
                    ? cardRect.Find("HeaderStrip").GetComponent<Image>()
                    : null;
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

        private void SetSelectedIndex(int index, bool force)
        {
            int clamped = Mathf.Clamp(index, 0, 2);
            if (!force && clamped == _selectedIndex)
            {
                return;
            }

            _selectedIndex = clamped;
            for (int i = 0; i < 3; i++)
            {
                bool selected = i == _selectedIndex;
                _targetScales[i] = selected ? SelectedCardScale : UnselectedCardScale;
                _targetTitleY[i] = selected ? SelectedTitleY : UnselectedTitleY;
                _targetSubtitleY[i] = selected ? SelectedSubY : UnselectedSubY;
                _targetDetailAlpha[i] = selected ? 1f : 0f;

                if (force)
                {
                    _titleCurrentY[i] = _targetTitleY[i];
                    _subtitleCurrentY[i] = _targetSubtitleY[i];
                    _detailCurrentAlpha[i] = _targetDetailAlpha[i];
                    ApplyTextPositions(i);
                    ApplyDetailAlpha(i, _detailCurrentAlpha[i]);
                }
            }
        }

        private void ConfirmSelection(int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            _onSelect?.Invoke(index);
        }

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

        private void TryUpdateHoverFromPointer()
        {
            Vector2 pointerPosition;
            if (!RuntimeInput.TryReadPointerScreenPosition(out pointerPosition))
            {
                return;
            }

            if (_suppressHoverUntilPointerMove)
            {
                if (_pointerAtKeySelect.x > -1000000f)
                {
                    Vector2 delta = pointerPosition - _pointerAtKeySelect;
                    if (delta.sqrMagnitude < HoverResumePointerMovePixels * HoverResumePointerMovePixels)
                    {
                        return;
                    }
                }

                _suppressHoverUntilPointerMove = false;
            }

            int hoveredIndex = -1;
            float bestCenterDistSq = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                RectTransform card = _optionCardRects[i];
                if (card == null)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(card, pointerPosition, null))
                {
                    continue;
                }

                Vector3 worldCenter = card.TransformPoint(card.rect.center);
                Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
                float distSq = (screenCenter - pointerPosition).sqrMagnitude;
                if (distSq < bestCenterDistSq)
                {
                    bestCenterDistSq = distSq;
                    hoveredIndex = i;
                }
            }

            if (hoveredIndex >= 0)
            {
                SetSelectedIndex(hoveredIndex, false);
            }
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
