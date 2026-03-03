using DeliveryRun.Delivery.Input;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed partial class MusicSelectionModalView
    {
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
                closeButton.onClick.AddListener(() =>
                {
                    ConfirmSelection(_selectedIndex >= 0 ? _selectedIndex : 0);
                });
            }
        }

        private void OnHovered(int index)
        {
            if (!_shown)
            {
                return;
            }

            _suppressHoverUntilPointerMove = false;
            SetSelectedIndex(index, false, SelectionInputSource.Pointer);
        }

        private void OnCardClicked(int index)
        {
            if (!_shown)
            {
                return;
            }

            _suppressHoverUntilPointerMove = false;
            SetSelectedIndex(index, false, SelectionInputSource.Pointer);
            ConfirmSelection(index);
        }

        private void SetSelectedIndex(int index, bool force, SelectionInputSource source)
        {
            int clamped = Mathf.Clamp(index, 0, 2);
            bool changed = clamped != _selectedIndex;
            if (!force && !changed)
            {
                if (source != SelectionInputSource.Unknown)
                {
                    _selectionInputSource = source;
                }

                RefreshSynergyPopupForSelection();
                return;
            }

            _selectedIndex = clamped;
            if (source != SelectionInputSource.Unknown)
            {
                _selectionInputSource = source;
            }

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

            RefreshSynergyPopupForSelection();
        }

        private void ConfirmSelection(int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            _onSelect?.Invoke(index);
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
                SetSelectedIndex(hoveredIndex, false, SelectionInputSource.Pointer);
            }
        }
    }
}
