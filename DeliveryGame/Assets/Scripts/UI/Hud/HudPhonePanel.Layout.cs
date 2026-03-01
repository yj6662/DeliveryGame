using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class HudPhonePanel
    {
        internal void UpdateLayout(float dt, PhoneViewState state)
        {
            if (_phonePanelRect == null)
            {
                return;
            }

            float activeTarget = ActiveOrderPanelBaseHeight + (state.ActiveOrderCount * ActiveOrderPanelRowHeight)
                                 + (GetDetailedOrderCardCountForLayout(state) * FoodDetailExtraHeight);
            _activePanelCurrentHeight = Mathf.MoveTowards(_activePanelCurrentHeight, activeTarget, PhoneSlideSpeed * dt);
            _phonePanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _activePanelCurrentHeight);

            if (_newOfferPanelRect == null)
            {
                _activePanelCurrentPosY = Mathf.MoveTowards(_activePanelCurrentPosY, ActiveOrderPanelBasePosY, PhoneSlideSpeed * dt);
                _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);
                return;
            }

            float previewTarget = state.PreviewVisible ? PhonePreviewHeight : PhoneBaseHeight;
            float targetLift = 0f;
            _phoneCurrentHeight = Mathf.MoveTowards(_phoneCurrentHeight, previewTarget, PhoneSlideSpeed * dt);
            _phoneCurrentLift = Mathf.MoveTowards(_phoneCurrentLift, targetLift, PhoneSlideSpeed * dt);
            _newOfferPanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _phoneCurrentHeight);
            _newOfferPanelRect.anchoredPosition = new Vector2(PhoneBasePosX, PhoneBasePosY);

            float offerTop = PhoneBasePosY + _phoneCurrentHeight;
            float activePosTargetY = Mathf.Max(ActiveOrderPanelBasePosY, offerTop + ActiveOrderPanelGapY);
            _activePanelCurrentPosY = Mathf.MoveTowards(_activePanelCurrentPosY, activePosTargetY, PhoneSlideSpeed * dt);
            _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);

            UpdateNewOfferProgressOverlay(state);
        }

        internal void RebuildTextIfNeeded(bool force, PhoneViewState state)
        {
            if (!force && !_dirty)
            {
                return;
            }

            if (_phoneHeaderText == null || _phonePreviewText == null)
            {
                return;
            }

            _dirty = false;

            _phoneHeaderText.text = state.ActiveOrderCount > 0
                ? "ACTIVE ORDERS  " + state.ActiveOrderCount
                : "ACTIVE ORDERS  0";

            RefreshActiveOrderCards(state);

            if (_newOfferHeaderText != null)
            {
                _newOfferHeaderText.text = state.PreviewVisible ? "NEW ORDER" : "INCOMING ORDER";
            }

            if (state.PreviewVisible)
            {
                _builder.Clear();
                _builder.Append("<color=#FFD57A>NEW ORDER</color>");
                if (!string.IsNullOrEmpty(state.CurrentPickupName))
                {
                    _builder.Append('\n').Append("Pickup  ").Append(state.CurrentPickupName);
                }

                if (!string.IsNullOrEmpty(state.CurrentDeliveryName))
                {
                    _builder.Append('\n').Append("Dropoff ").Append(state.CurrentDeliveryName);
                }

                _builder.Append('\n').Append("Base Reward  $").Append(state.CurrentOfferReward);
                _builder.Append('\n').Append("Accept TTL   ").Append(state.CurrentOfferRemaining.ToString("0.0")).Append("s");
                _builder.Append('\n').Append("<color=#9CD2FF>[SPACE]</color> Accept");
                _phonePreviewText.text = _builder.ToString();
                _phonePreviewText.gameObject.SetActive(true);
            }
            else
            {
                _phonePreviewText.text = "Waiting for offer...";
                _phonePreviewText.gameObject.SetActive(true);
            }
        }

        internal void SetDirty()
        {
            _dirty = true;
        }

        internal void ResetRuntimeState()
        {
            _phoneCurrentHeight = PhoneBaseHeight;
            _phoneCurrentLift = 0f;
            _activePanelCurrentHeight = ActiveOrderPanelBaseHeight;
            _activePanelCurrentPosY = ActiveOrderPanelBasePosY;
            _dirty = true;

            if (_phonePanelRect != null)
            {
                _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);
                _phonePanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _activePanelCurrentHeight);
            }

            if (_newOfferPanelRect != null)
            {
                _newOfferPanelRect.anchoredPosition = new Vector2(PhoneBasePosX, PhoneBasePosY);
                _newOfferPanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _phoneCurrentHeight);
            }

            if (_newOfferExpiryOverlay != null && _newOfferExpiryOverlay.gameObject.activeSelf)
            {
                _newOfferExpiryOverlay.gameObject.SetActive(false);
            }
        }

        internal void Cleanup()
        {
            _phonePanelRect = null;
            _phoneHeaderText = null;
            _phoneActiveListText = null;
            _phoneActiveCardsRoot = null;
            for (int i = 0; i < _maxTrackedOrders; i++)
            {
                _phoneOrderCardRects[i] = null;
                _phoneOrderCardTexts[i] = null;
                _phoneOrderDetailRoots[i] = null;
                _phoneOrderTempFillImages[i] = null;
                _phoneOrderSpillFillImages[i] = null;
                _phoneOrderQualityFillImages[i] = null;
                _phoneOrderTempValueTexts[i] = null;
                _phoneOrderSpillValueTexts[i] = null;
                _phoneOrderQualityValueTexts[i] = null;
            }

            _newOfferPanelRect = null;
            _newOfferHeaderText = null;
            _phonePreviewText = null;
            _newOfferExpiryOverlay = null;
            _newOfferExpiryOverlayRect = null;
            _dirty = true;
            _phoneCurrentHeight = PhoneBaseHeight;
            _phoneCurrentLift = 0f;
            _activePanelCurrentHeight = ActiveOrderPanelBaseHeight;
            _activePanelCurrentPosY = ActiveOrderPanelBasePosY;
        }

        private void UpdateNewOfferProgressOverlay(PhoneViewState state)
        {
            if (_newOfferExpiryOverlay == null || _newOfferExpiryOverlayRect == null)
            {
                return;
            }

            bool show = state.PreviewVisible && state.OfferAcceptWindow && state.CurrentOfferDuration > 0.001f;
            if (!show)
            {
                if (_newOfferExpiryOverlay.gameObject.activeSelf)
                {
                    _newOfferExpiryOverlay.gameObject.SetActive(false);
                }

                _newOfferExpiryOverlayRect.anchorMin = new Vector2(0f, 1f);
                _newOfferExpiryOverlayRect.anchorMax = new Vector2(1f, 1f);
                _newOfferExpiryOverlayRect.offsetMin = Vector2.zero;
                _newOfferExpiryOverlayRect.offsetMax = Vector2.zero;
                return;
            }

            float normalized = Mathf.Clamp01(state.CurrentOfferRemaining / state.CurrentOfferDuration);
            if (!_newOfferExpiryOverlay.gameObject.activeSelf)
            {
                _newOfferExpiryOverlay.gameObject.SetActive(true);
            }

            _newOfferExpiryOverlayRect.anchorMin = new Vector2(0f, 1f - normalized);
            _newOfferExpiryOverlayRect.anchorMax = new Vector2(1f, 1f);
            _newOfferExpiryOverlayRect.offsetMin = Vector2.zero;
            _newOfferExpiryOverlayRect.offsetMax = Vector2.zero;
        }
    }
}
