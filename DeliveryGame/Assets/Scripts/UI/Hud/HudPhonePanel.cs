using System;
using System.Collections.Generic;
using System.Text;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudPhonePanel
    {
        internal struct PhoneViewState
        {
            public int ActiveOrderCount;
            public bool HasFoodState;
            public float FoodTemperature01;
            public float FoodSpill01;
            public string FoodOfferId;

            public bool PreviewVisible;
            public bool OfferAcceptWindow;
            public float CurrentOfferDuration;
            public float CurrentOfferRemaining;
            public string CurrentPickupName;
            public string CurrentDeliveryName;
            public int CurrentOfferReward;
        }

        private const float PhoneWidth = 352f;
        private const float PhoneBaseHeight = 92f;
        private const float PhonePreviewHeight = 188f;
        private const float PhoneSlideSpeed = 760f;
        private const float ActiveOrderPanelWidth = 372f;
        private const float ActiveOrderPanelBaseHeight = 104f;
        private const float ActiveOrderPanelRowHeight = 60f;
        private const float ActiveOrderPanelPosX = -30f;
        private const float ActiveOrderPanelBasePosY = 228f;
        private const float ActiveOrderPanelGapY = 20f;
        private const float PhoneBasePosX = -34f;
        private const float PhoneBasePosY = 28f;
        private const float PhonePreviewLiftY = 124f;
        private const int GaugeBarSegments = 10;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private readonly HudActiveOrderStore _activeOrders;
        private readonly Dictionary<string, bool> _orderCarryingByOffer;
        private readonly int _maxTrackedOrders;

        private RectTransform _phonePanelRect;
        private Text _phoneHeaderText;
        private Text _phoneActiveListText;
        private RectTransform _phoneActiveCardsRoot;
        private readonly RectTransform[] _phoneOrderCardRects;
        private readonly Text[] _phoneOrderCardTexts;
        private RectTransform _newOfferPanelRect;
        private Text _newOfferHeaderText;
        private Text _phonePreviewText;
        private Image _newOfferExpiryOverlay;
        private RectTransform _newOfferExpiryOverlayRect;

        private float _phoneCurrentHeight = PhoneBaseHeight;
        private float _phoneCurrentLift;
        private float _activePanelCurrentHeight = ActiveOrderPanelBaseHeight;
        private float _activePanelCurrentPosY = ActiveOrderPanelBasePosY;
        private bool _dirty = true;

        internal HudPhonePanel(HudActiveOrderStore activeOrders, Dictionary<string, bool> orderCarryingByOffer)
        {
            _activeOrders = activeOrders ?? new HudActiveOrderStore(1);
            _orderCarryingByOffer = orderCarryingByOffer;
            _maxTrackedOrders = Mathf.Min(_activeOrders.Ids.Length, _activeOrders.Texts.Length);
            _phoneOrderCardRects = new RectTransform[_maxTrackedOrders];
            _phoneOrderCardTexts = new Text[_maxTrackedOrders];
        }

        internal void BuildIfNeeded(RunHudView view)
        {
            if (view == null)
            {
                return;
            }

            RectTransform root = view.GetRootRectTransform();
            if (root == null || _phonePanelRect != null)
            {
                return;
            }

            _activePanelCurrentPosY = ActiveOrderPanelBasePosY;
            _activePanelCurrentHeight = ActiveOrderPanelBaseHeight;
            _phoneCurrentLift = 0f;
            _phoneCurrentHeight = PhoneBaseHeight;

            Sprite panelSprite = view.GetPanelSkinSprite();
            Font defaultFont = view.GetDefaultFont();

            GameObject activePanelObject = new GameObject("ActiveOrdersPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _phonePanelRect = activePanelObject.GetComponent<RectTransform>();
            _phonePanelRect.SetParent(root, false);
            _phonePanelRect.anchorMin = new Vector2(1f, 0f);
            _phonePanelRect.anchorMax = new Vector2(1f, 0f);
            _phonePanelRect.pivot = new Vector2(1f, 0f);
            _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);
            _phonePanelRect.sizeDelta = new Vector2(ActiveOrderPanelWidth, _activePanelCurrentHeight);

            Image phonePanelImage = activePanelObject.GetComponent<Image>();
            phonePanelImage.sprite = panelSprite;
            phonePanelImage.type = phonePanelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            phonePanelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            phonePanelImage.raycastTarget = false;

            GameObject activeScreenObject = new GameObject("Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform activeScreenRect = activeScreenObject.GetComponent<RectTransform>();
            activeScreenRect.SetParent(_phonePanelRect, false);
            HudUiFactory.AnchorStretch(activeScreenRect, 8f, 8f, 10f, 10f);
            Image activeScreenImage = activeScreenObject.GetComponent<Image>();
            activeScreenImage.color = new Color(0.12f, 0.16f, 0.2f, 0.96f);
            activeScreenImage.raycastTarget = false;

            _phoneHeaderText = HudUiFactory.CreateText("Header", activeScreenRect, defaultFont, 18, TextAnchor.UpperLeft);
            HudUiFactory.AnchorStretchTop(_phoneHeaderText.rectTransform, 14f, 14f, 12f, 28f);
            _phoneHeaderText.color = new Color(0.96f, 0.98f, 1f, 1f);

            _phoneActiveListText = HudUiFactory.CreateText("ActiveList", activeScreenRect, defaultFont, 15, TextAnchor.UpperLeft);
            HudUiFactory.AnchorStretch(_phoneActiveListText.rectTransform, 14f, 14f, 12f, 44f);
            _phoneActiveListText.verticalOverflow = VerticalWrapMode.Overflow;
            _phoneActiveListText.lineSpacing = 1.06f;
            _phoneActiveListText.gameObject.SetActive(false);

            _phoneActiveCardsRoot = new GameObject("OrderCardsRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            _phoneActiveCardsRoot.SetParent(activeScreenRect, false);
            HudUiFactory.AnchorStretch(_phoneActiveCardsRoot, 12f, 12f, 10f, 40f);

            for (int i = 0; i < _maxTrackedOrders; i++)
            {
                GameObject cardObject = new GameObject(
                    "OrderCard_" + i.ToString("00"),
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

                RectTransform cardRect = cardObject.GetComponent<RectTransform>();
                cardRect.SetParent(_phoneActiveCardsRoot, false);
                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(1f, 1f);
                cardRect.pivot = new Vector2(0.5f, 1f);
                cardRect.sizeDelta = new Vector2(0f, ActiveOrderPanelRowHeight - 6f);
                cardRect.anchoredPosition = new Vector2(0f, -(i * ActiveOrderPanelRowHeight));

                Image cardImage = cardObject.GetComponent<Image>();
                cardImage.sprite = panelSprite;
                cardImage.type = cardImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                cardImage.color = new Color(0.13f, 0.18f, 0.24f, 0.94f);
                cardImage.raycastTarget = false;

                Text cardText = HudUiFactory.CreateText("Text", cardRect, defaultFont, 15, TextAnchor.UpperLeft);
                HudUiFactory.AnchorStretch(cardText.rectTransform, 10f, 10f, 8f, 8f);
                cardText.lineSpacing = 1.05f;
                cardText.color = new Color(0.96f, 0.98f, 1f, 1f);

                _phoneOrderCardRects[i] = cardRect;
                _phoneOrderCardTexts[i] = cardText;
                cardObject.SetActive(false);
            }

            GameObject offerPanelObject = new GameObject("NewOfferPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _newOfferPanelRect = offerPanelObject.GetComponent<RectTransform>();
            _newOfferPanelRect.SetParent(root, false);
            _newOfferPanelRect.anchorMin = new Vector2(1f, 0f);
            _newOfferPanelRect.anchorMax = new Vector2(1f, 0f);
            _newOfferPanelRect.pivot = new Vector2(1f, 0f);
            _newOfferPanelRect.anchoredPosition = new Vector2(PhoneBasePosX, PhoneBasePosY);
            _newOfferPanelRect.sizeDelta = new Vector2(PhoneWidth, _phoneCurrentHeight);

            Image newOfferPanelImage = offerPanelObject.GetComponent<Image>();
            newOfferPanelImage.sprite = panelSprite;
            newOfferPanelImage.type = newOfferPanelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            newOfferPanelImage.color = new Color(0.09f, 0.1f, 0.14f, 0.97f);
            newOfferPanelImage.raycastTarget = false;

            GameObject offerScreenObject = new GameObject("Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform offerScreenRect = offerScreenObject.GetComponent<RectTransform>();
            offerScreenRect.SetParent(_newOfferPanelRect, false);
            HudUiFactory.AnchorStretch(offerScreenRect, 8f, 8f, 10f, 10f);
            Image offerScreenImage = offerScreenObject.GetComponent<Image>();
            offerScreenImage.color = new Color(0.12f, 0.16f, 0.2f, 0.96f);
            offerScreenImage.raycastTarget = false;

            _newOfferHeaderText = HudUiFactory.CreateText("Header", offerScreenRect, defaultFont, 18, TextAnchor.UpperLeft);
            HudUiFactory.AnchorStretchTop(_newOfferHeaderText.rectTransform, 14f, 14f, 12f, 28f);
            _newOfferHeaderText.color = new Color(0.96f, 0.98f, 1f, 1f);

            _phonePreviewText = HudUiFactory.CreateText("Preview", offerScreenRect, defaultFont, 17, TextAnchor.MiddleLeft);
            HudUiFactory.AnchorStretch(_phonePreviewText.rectTransform, 14f, 14f, 12f, 44f);
            _phonePreviewText.lineSpacing = 1.08f;

            GameObject overlayObject = new GameObject("OfferExpiryOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(offerScreenRect, false);
            HudUiFactory.AnchorStretch(overlayRect, 0f, 0f, 0f, 0f);
            _newOfferExpiryOverlayRect = overlayRect;
            _newOfferExpiryOverlay = overlayObject.GetComponent<Image>();
            _newOfferExpiryOverlay.color = new Color(0f, 0f, 0f, 0.96f);
            Sprite overlaySprite = panelSprite ?? Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            _newOfferExpiryOverlay.sprite = overlaySprite;
            _newOfferExpiryOverlay.type = _newOfferExpiryOverlay.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            _newOfferExpiryOverlay.raycastTarget = false;
            _newOfferExpiryOverlay.gameObject.SetActive(false);

            _dirty = true;
        }

        internal void UpdateLayout(float dt, PhoneViewState state)
        {
            if (_phonePanelRect == null)
            {
                return;
            }

            float activeTarget = ActiveOrderPanelBaseHeight + (state.ActiveOrderCount * ActiveOrderPanelRowHeight)
                                 + (GetDetailedOrderCardCountForLayout(state) * 52f);
            _activePanelCurrentHeight = Mathf.MoveTowards(_activePanelCurrentHeight, activeTarget, PhoneSlideSpeed * dt);
            _phonePanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _activePanelCurrentHeight);

            if (_newOfferPanelRect == null)
            {
                _activePanelCurrentPosY = Mathf.MoveTowards(_activePanelCurrentPosY, ActiveOrderPanelBasePosY, PhoneSlideSpeed * dt);
                _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);
                return;
            }

            float previewTarget = state.PreviewVisible ? PhonePreviewHeight : PhoneBaseHeight;
            float targetLift = state.PreviewVisible ? PhonePreviewLiftY : 0f;
            _phoneCurrentHeight = Mathf.MoveTowards(_phoneCurrentHeight, previewTarget, PhoneSlideSpeed * dt);
            _phoneCurrentLift = Mathf.MoveTowards(_phoneCurrentLift, targetLift, PhoneSlideSpeed * dt);
            _newOfferPanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _phoneCurrentHeight);
            _newOfferPanelRect.anchoredPosition = new Vector2(PhoneBasePosX, PhoneBasePosY + _phoneCurrentLift);

            float offerTop = (PhoneBasePosY + _phoneCurrentLift) + _phoneCurrentHeight;
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
                if (state.HasFoodState)
                {
                    _builder.Append('\n').Append("Temp ").Append(Mathf.RoundToInt(state.FoodTemperature01 * 100f)).Append("%");
                    _builder.Append("  Spill ").Append(Mathf.RoundToInt(state.FoodSpill01 * 100f)).Append("%");
                }

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
            bool showFoodOnAllCarrying = ShouldShowFoodDetailsForAllCarryingSlots(state);
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
                    continue;
                }

                float rowHeight = ActiveOrderPanelRowHeight - 6f;
                _builder.Clear();
                if (showFallback)
                {
                    _builder.Append("No active orders");
                }
                else
                {
                    string text = i < activeOrderTextsRef.Length ? activeOrderTextsRef[i] : string.Empty;
                    _builder.Append("#").Append(i + 1).Append("  ").Append(text);

                    bool isFoodOrder = false;
                    if (state.HasFoodState)
                    {
                        string offerId = i < activeOrderIdsRef.Length ? activeOrderIdsRef[i] : null;
                        if (showFoodOnAllCarrying)
                        {
                            isFoodOrder = IsOfferCarrying(offerId);
                        }
                        else if (!string.IsNullOrEmpty(state.FoodOfferId))
                        {
                            isFoodOrder = string.Equals(offerId, state.FoodOfferId, StringComparison.Ordinal);
                        }
                        else
                        {
                            isFoodOrder = i == 0;
                        }
                    }

                    if (isFoodOrder)
                    {
                        _builder.Append('\n');
                        AppendFoodGaugeLine(_builder, "TEMP", state.FoodTemperature01, true);
                        _builder.Append('\n');
                        AppendFoodGaugeLine(_builder, "SPILL", state.FoodSpill01, false);
                        rowHeight += 52f;
                    }
                }

                cardText.text = _builder.ToString();
                cardRect.sizeDelta = new Vector2(0f, rowHeight);
                cardRect.anchoredPosition = new Vector2(0f, -y);
                y += rowHeight + 6f;
            }
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

        private bool ShouldShowFoodDetailsForAllCarryingSlots(PhoneViewState state)
        {
            if (!state.HasFoodState || state.ActiveOrderCount != 3)
            {
                return false;
            }

            string[] activeOrderIdsRef = _activeOrders.Ids;
            for (int i = 0; i < state.ActiveOrderCount; i++)
            {
                if (!IsOfferCarrying(i < activeOrderIdsRef.Length ? activeOrderIdsRef[i] : null))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetDetailedOrderCardCountForLayout(PhoneViewState state)
        {
            if (!state.HasFoodState || state.ActiveOrderCount <= 0)
            {
                return 0;
            }

            if (!ShouldShowFoodDetailsForAllCarryingSlots(state))
            {
                return 1;
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

        private static void AppendFoodGaugeLine(StringBuilder builder, string label, float value01, bool higherIsBetter)
        {
            float value = Mathf.Clamp01(value01);
            float score = higherIsBetter ? value : (1f - value);
            int fillCount = Mathf.RoundToInt(value * GaugeBarSegments);
            fillCount = Mathf.Clamp(fillCount, 0, GaugeBarSegments);

            string stateLabel;
            string colorTag;
            if (score >= 0.66f)
            {
                stateLabel = "GOOD";
                colorTag = "#6CFF6C";
            }
            else if (score >= 0.33f)
            {
                stateLabel = "CAUTION";
                colorTag = "#FFD34D";
            }
            else
            {
                stateLabel = "RISK";
                colorTag = "#FF5B5B";
            }

            builder.Append(label).Append(' ').Append('[');
            for (int i = 0; i < GaugeBarSegments; i++)
            {
                builder.Append(i < fillCount ? '|' : '-');
            }

            builder.Append(']')
                .Append(' ')
                .Append("<color=").Append(colorTag).Append('>')
                .Append(stateLabel)
                .Append("</color>")
                .Append(' ')
                .Append(Mathf.RoundToInt(value * 100f)).Append('%');
        }
    }
}
