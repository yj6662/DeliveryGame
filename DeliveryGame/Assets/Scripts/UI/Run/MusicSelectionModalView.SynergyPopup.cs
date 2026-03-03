using DeliveryRun.Delivery.Input;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed partial class MusicSelectionModalView
    {
        private const float SynergyPopupWidth = 420f;
        private const float SynergyPopupHeight = 168f;
        private const float SynergyPopupEdgePadding = 20f;
        private const float SynergyPopupKeyboardOverlapY = 10f;
        private const float SynergyPopupPointerOffsetX = 18f;
        private const float SynergyPopupPointerOffsetY = -18f;
        private const float SynergyPopupMoveLerpSpeed = 16f;

        private enum SelectionInputSource
        {
            Unknown = 0,
            Pointer = 1,
            Keyboard = 2
        }

        private readonly string[] _optionSynergyPreviewTexts = new string[3];
        private readonly bool[] _optionCanActivateSynergyNow = new bool[3];

        private RectTransform _synergyPopupRect;
        private Text _synergyPopupText;
        private bool _synergyPopupVisible;
        private Vector2 _synergyPopupTargetAnchoredPosition;
        private SelectionInputSource _selectionInputSource;

        private void RefreshSynergyPopupForSelection()
        {
            if (!_shown || _selectedIndex < 0 || _selectedIndex >= 3)
            {
                HideSynergyPopup();
                return;
            }

            if (_selectionInputSource == SelectionInputSource.Unknown)
            {
                HideSynergyPopup();
                return;
            }

            if (!_optionCanActivateSynergyNow[_selectedIndex])
            {
                HideSynergyPopup();
                return;
            }

            string popupText = _optionSynergyPreviewTexts[_selectedIndex];
            if (string.IsNullOrEmpty(popupText))
            {
                HideSynergyPopup();
                return;
            }

            EnsureSynergyPopupCreated();
            if (_synergyPopupRect == null || _synergyPopupText == null)
            {
                HideSynergyPopup();
                return;
            }

            _synergyPopupText.text = popupText;
            _synergyPopupVisible = true;
            if (!_synergyPopupRect.gameObject.activeSelf)
            {
                _synergyPopupRect.gameObject.SetActive(true);
            }

            _synergyPopupTargetAnchoredPosition = ResolveSynergyPopupTargetPosition();
            _synergyPopupRect.anchoredPosition = _synergyPopupTargetAnchoredPosition;
        }

        private void UpdateSynergyPopup(float dt)
        {
            if (!_shown || !_synergyPopupVisible || _synergyPopupRect == null)
            {
                return;
            }

            _synergyPopupTargetAnchoredPosition = ResolveSynergyPopupTargetPosition();
            if (dt <= 0f)
            {
                _synergyPopupRect.anchoredPosition = _synergyPopupTargetAnchoredPosition;
                return;
            }

            float t = 1f - Mathf.Exp(-SynergyPopupMoveLerpSpeed * dt);
            _synergyPopupRect.anchoredPosition = Vector2.Lerp(
                _synergyPopupRect.anchoredPosition,
                _synergyPopupTargetAnchoredPosition,
                t);
        }

        private void HideSynergyPopup()
        {
            _synergyPopupVisible = false;
            if (_synergyPopupRect != null && _synergyPopupRect.gameObject.activeSelf)
            {
                _synergyPopupRect.gameObject.SetActive(false);
            }
        }

        private void EnsureSynergyPopupCreated()
        {
            if (_synergyPopupRect != null)
            {
                return;
            }

            RectTransform parent = transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            GameObject root = new GameObject(
                "SynergyPreviewPopup",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _synergyPopupRect = root.GetComponent<RectTransform>();
            _synergyPopupRect.SetParent(parent, false);
            _synergyPopupRect.anchorMin = new Vector2(0.5f, 0.5f);
            _synergyPopupRect.anchorMax = new Vector2(0.5f, 0.5f);
            _synergyPopupRect.pivot = new Vector2(0.5f, 1f);
            _synergyPopupRect.sizeDelta = new Vector2(SynergyPopupWidth, SynergyPopupHeight);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.04f, 0.08f, 0.12f, 0.96f);
            background.raycastTarget = false;

            GameObject textGo = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(_synergyPopupRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 12f);
            textRect.offsetMax = new Vector2(-14f, -12f);

            _synergyPopupText = textGo.GetComponent<Text>();
            _synergyPopupText.font = ResolvePopupFont();
            _synergyPopupText.fontSize = 16;
            _synergyPopupText.fontStyle = FontStyle.Bold;
            _synergyPopupText.alignment = TextAnchor.UpperLeft;
            _synergyPopupText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _synergyPopupText.verticalOverflow = VerticalWrapMode.Overflow;
            _synergyPopupText.lineSpacing = 1.08f;
            _synergyPopupText.color = new Color(0.95f, 0.98f, 1f, 1f);
            _synergyPopupText.raycastTarget = false;

            _synergyPopupRect.gameObject.SetActive(false);
        }

        private Font ResolvePopupFont()
        {
            if (optionSynergyTexts != null)
            {
                for (int i = 0; i < optionSynergyTexts.Length; i++)
                {
                    if (optionSynergyTexts[i] != null && optionSynergyTexts[i].font != null)
                    {
                        return optionSynergyTexts[i].font;
                    }
                }
            }

            if (titleText != null && titleText.font != null)
            {
                return titleText.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Vector2 ResolveSynergyPopupTargetPosition()
        {
            RectTransform rootRect = transform as RectTransform;
            if (rootRect == null || _selectedIndex < 0 || _selectedIndex >= 3)
            {
                return Vector2.zero;
            }

            Vector2 localPos = Vector2.zero;
            bool fromPointer = false;
            if (_selectionInputSource == SelectionInputSource.Pointer)
            {
                Vector2 pointerScreenPos;
                RectTransform selectedCard = _optionCardRects[_selectedIndex];
                if (selectedCard != null &&
                    RuntimeInput.TryReadPointerScreenPosition(out pointerScreenPos) &&
                    RectTransformUtility.RectangleContainsScreenPoint(selectedCard, pointerScreenPos, null) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rootRect,
                        pointerScreenPos,
                        null,
                        out localPos))
                {
                    localPos.x += SynergyPopupPointerOffsetX;
                    localPos.y += SynergyPopupPointerOffsetY;
                    fromPointer = true;
                }
            }

            if (!fromPointer)
            {
                RectTransform card = _optionCardRects[_selectedIndex];
                if (card == null)
                {
                    return ClampSynergyPopupPosition(rootRect, Vector2.zero);
                }

                Vector3 worldBottomCenter = card.TransformPoint(new Vector3(0f, card.rect.yMin, 0f));
                Vector2 bottomScreenPos = RectTransformUtility.WorldToScreenPoint(null, worldBottomCenter);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rootRect,
                        bottomScreenPos,
                        null,
                        out localPos))
                {
                    localPos = Vector2.zero;
                }

                // Place below card while slightly overlapping the card bottom edge.
                localPos.y += SynergyPopupKeyboardOverlapY;
            }

            return ClampSynergyPopupPosition(rootRect, localPos);
        }

        private Vector2 ClampSynergyPopupPosition(RectTransform rootRect, Vector2 desired)
        {
            if (_synergyPopupRect == null)
            {
                return desired;
            }

            Rect bounds = rootRect.rect;
            Vector2 popupSize = _synergyPopupRect.sizeDelta;
            float halfWidth = popupSize.x * 0.5f;
            float clampedX = Mathf.Clamp(
                desired.x,
                bounds.xMin + halfWidth + SynergyPopupEdgePadding,
                bounds.xMax - halfWidth - SynergyPopupEdgePadding);
            float clampedY = Mathf.Clamp(
                desired.y,
                bounds.yMin + popupSize.y + SynergyPopupEdgePadding,
                bounds.yMax - SynergyPopupEdgePadding);
            return new Vector2(clampedX, clampedY);
        }
    }
}
