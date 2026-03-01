using System;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class HudPhonePanel
    {
        private void RefreshActiveOrderCards(PhoneViewState state)
        {
            if (_maxTrackedOrders <= 0 || _phoneOrderCardRects[0] == null || _phoneOrderCardTexts[0] == null)
            {
                if (_phoneActiveListText != null)
                {
                    _phoneActiveListText.gameObject.SetActive(true);
                    string[] activeOrderTexts = _activeOrders.Texts;
                    _phoneActiveListText.text = state.ActiveOrderCount <= 0
                        ? "No active orders"
                        : (activeOrderTexts.Length > 0 ? activeOrderTexts[0] : "No active orders");
                }

                return;
            }

            if (_phoneActiveListText != null)
            {
                _phoneActiveListText.gameObject.SetActive(false);
            }

            float y = 0f;
            int visibleCount = state.ActiveOrderCount;
            bool showFallback = visibleCount <= 0;
            string[] activeOrderTextsRef = _activeOrders.Texts;
            string[] activeOrderIdsRef = _activeOrders.Ids;
            if (showFallback)
            {
                visibleCount = 1;
            }

            for (int i = 0; i < _maxTrackedOrders; i++)
            {
                RectTransform cardRect = _phoneOrderCardRects[i];
                Text cardText = _phoneOrderCardTexts[i];
                if (cardRect == null || cardText == null)
                {
                    continue;
                }

                bool visible = i < visibleCount;
                cardRect.gameObject.SetActive(visible);
                if (!visible)
                {
                    UpdateFoodDetailUi(i, false, null);
                    continue;
                }

                float rowHeight = ActiveOrderPanelRowHeight - 6f;
                _builder.Clear();
                string offerId = i < activeOrderIdsRef.Length ? activeOrderIdsRef[i] : null;
                bool showFoodDetails = false;
                if (showFallback)
                {
                    _builder.Append("No active orders");
                }
                else
                {
                    string text = i < activeOrderTextsRef.Length ? activeOrderTextsRef[i] : string.Empty;
                    _builder.Append("#").Append(i + 1).Append("  ").Append(text);
                    showFoodDetails = IsOfferCarrying(offerId);
                    if (showFoodDetails)
                    {
                        rowHeight += FoodDetailExtraHeight;
                    }
                }

                cardText.text = _builder.ToString();
                UpdateFoodDetailUi(i, showFoodDetails, offerId);
                cardRect.sizeDelta = new Vector2(0f, rowHeight);
                cardRect.anchoredPosition = new Vector2(0f, -y);
                y += rowHeight + 6f;
            }
        }

        private int GetDetailedOrderCardCountForLayout(PhoneViewState state)
        {
            if (state.ActiveOrderCount <= 0)
            {
                return 0;
            }

            string[] activeOrderIdsRef = _activeOrders.Ids;
            int carryingCount = 0;
            for (int i = 0; i < state.ActiveOrderCount; i++)
            {
                if (IsOfferCarrying(i < activeOrderIdsRef.Length ? activeOrderIdsRef[i] : null))
                {
                    carryingCount++;
                }
            }

            return carryingCount;
        }

        private bool IsOfferCarrying(string offerId)
        {
            if (string.IsNullOrEmpty(offerId) || _orderCarryingByOffer == null)
            {
                return false;
            }

            bool isCarrying;
            if (_orderCarryingByOffer.TryGetValue(offerId, out isCarrying))
            {
                return isCarrying;
            }

            return false;
        }

        private void UpdateFoodDetailUi(int index, bool visible, string offerId)
        {
            if (index < 0 || index >= _maxTrackedOrders)
            {
                return;
            }

            RectTransform detailRoot = _phoneOrderDetailRoots[index];
            if (detailRoot == null)
            {
                return;
            }

            detailRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            float temperature01 = 1f;
            float spill01 = 0f;
            float quality01 = 1f;
            if (!string.IsNullOrEmpty(offerId) &&
                _foodStateByOffer != null &&
                _foodStateByOffer.TryGetValue(offerId, out FoodStateTicked snapshot))
            {
                temperature01 = Mathf.Clamp01(snapshot.Temperature01);
                spill01 = Mathf.Clamp01(snapshot.Spill01);
                quality01 = Mathf.Clamp01(snapshot.Quality01);
            }

            ApplyFoodBar(
                _phoneOrderTempFillImages[index],
                _phoneOrderTempValueTexts[index],
                temperature01,
                new Color(1f, 0.66f, 0.25f, 1f),
                new Color(0.35f, 0.82f, 1f, 1f));

            ApplyFoodBar(
                _phoneOrderSpillFillImages[index],
                _phoneOrderSpillValueTexts[index],
                spill01,
                new Color(0.36f, 1f, 0.42f, 1f),
                new Color(1f, 0.36f, 0.36f, 1f));

            ApplyFoodBar(
                _phoneOrderQualityFillImages[index],
                _phoneOrderQualityValueTexts[index],
                quality01,
                new Color(1f, 0.36f, 0.36f, 1f),
                new Color(0.36f, 1f, 0.42f, 1f));
        }

        private static void ApplyFoodBar(Image fill, Text valueText, float value01, Color lowColor, Color highColor)
        {
            if (fill == null)
            {
                return;
            }

            float value = Mathf.Clamp01(value01);
            RectTransform fillRect = fill.rectTransform;
            if (fillRect != null)
            {
                fillRect.anchorMax = new Vector2(value, 1f);
            }

            fill.color = Color.Lerp(lowColor, highColor, value);
            if (valueText != null)
            {
                valueText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }
    }
}
