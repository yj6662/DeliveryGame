using System;
using System.Collections.Generic;
using System.Text;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class HudPhonePanel
    {
        internal struct PhoneViewState
        {
            public int ActiveOrderCount;
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
        private const float FoodDetailExtraHeight = 66f;
        private const float FoodDetailPanelHeight = 58f;
        private const float FoodDetailRowHeight = 16f;
        private const float FoodDetailRowGap = 3f;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private readonly HudActiveOrderStore _activeOrders;
        private readonly Dictionary<string, bool> _orderCarryingByOffer;
        private readonly Dictionary<string, FoodStateTicked> _foodStateByOffer;
        private readonly int _maxTrackedOrders;

        private RectTransform _phonePanelRect;
        private Text _phoneHeaderText;
        private Text _phoneActiveListText;
        private RectTransform _phoneActiveCardsRoot;
        private readonly RectTransform[] _phoneOrderCardRects;
        private readonly Text[] _phoneOrderCardTexts;
        private readonly RectTransform[] _phoneOrderDetailRoots;
        private readonly Image[] _phoneOrderTempFillImages;
        private readonly Image[] _phoneOrderSpillFillImages;
        private readonly Image[] _phoneOrderQualityFillImages;
        private readonly Text[] _phoneOrderTempValueTexts;
        private readonly Text[] _phoneOrderSpillValueTexts;
        private readonly Text[] _phoneOrderQualityValueTexts;
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

        internal HudPhonePanel(
            HudActiveOrderStore activeOrders,
            Dictionary<string, bool> orderCarryingByOffer,
            Dictionary<string, FoodStateTicked> foodStateByOffer)
        {
            _activeOrders = activeOrders ?? new HudActiveOrderStore(1);
            _orderCarryingByOffer = orderCarryingByOffer;
            _foodStateByOffer = foodStateByOffer;
            _maxTrackedOrders = Mathf.Min(_activeOrders.Ids.Length, _activeOrders.Texts.Length);
            _phoneOrderCardRects = new RectTransform[_maxTrackedOrders];
            _phoneOrderCardTexts = new Text[_maxTrackedOrders];
            _phoneOrderDetailRoots = new RectTransform[_maxTrackedOrders];
            _phoneOrderTempFillImages = new Image[_maxTrackedOrders];
            _phoneOrderSpillFillImages = new Image[_maxTrackedOrders];
            _phoneOrderQualityFillImages = new Image[_maxTrackedOrders];
            _phoneOrderTempValueTexts = new Text[_maxTrackedOrders];
            _phoneOrderSpillValueTexts = new Text[_maxTrackedOrders];
            _phoneOrderQualityValueTexts = new Text[_maxTrackedOrders];
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
                HudUiFactory.AnchorStretchTop(cardText.rectTransform, 10f, 10f, 8f, 24f);
                cardText.horizontalOverflow = HorizontalWrapMode.Overflow;
                cardText.verticalOverflow = VerticalWrapMode.Overflow;
                cardText.lineSpacing = 1.05f;
                cardText.color = new Color(0.96f, 0.98f, 1f, 1f);

                CreateFoodDetailRows(i, cardRect, defaultFont);

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

        private void CreateFoodDetailRows(int index, RectTransform parent, Font font)
        {
            GameObject detailsRootObject = new GameObject("FoodDetail", typeof(RectTransform));
            RectTransform detailsRoot = detailsRootObject.GetComponent<RectTransform>();
            detailsRoot.SetParent(parent, false);
            detailsRoot.anchorMin = new Vector2(0f, 0f);
            detailsRoot.anchorMax = new Vector2(1f, 0f);
            detailsRoot.pivot = new Vector2(0.5f, 0f);
            detailsRoot.anchoredPosition = new Vector2(0f, 6f);
            detailsRoot.sizeDelta = new Vector2(0f, FoodDetailPanelHeight);

            CreateFoodDetailRow(
                detailsRoot, font, "TempRow", 0f, "TEMP",
                new Color(0.24f, 0.26f, 0.3f, 0.96f),
                out _phoneOrderTempFillImages[index],
                out _phoneOrderTempValueTexts[index]);

            CreateFoodDetailRow(
                detailsRoot, font, "SpillRow", FoodDetailRowHeight + FoodDetailRowGap, "SPILL",
                new Color(0.24f, 0.26f, 0.3f, 0.96f),
                out _phoneOrderSpillFillImages[index],
                out _phoneOrderSpillValueTexts[index]);

            CreateFoodDetailRow(
                detailsRoot, font, "QualityRow", (FoodDetailRowHeight + FoodDetailRowGap) * 2f, "QUALITY",
                new Color(0.24f, 0.26f, 0.3f, 0.96f),
                out _phoneOrderQualityFillImages[index],
                out _phoneOrderQualityValueTexts[index]);

            detailsRootObject.SetActive(false);
            _phoneOrderDetailRoots[index] = detailsRoot;
        }

        private static void CreateFoodDetailRow(
            RectTransform parent,
            Font font,
            string rowName,
            float topOffset,
            string label,
            Color barBackgroundColor,
            out Image fillImage,
            out Text valueText)
        {
            GameObject rowObject = new GameObject(rowName, typeof(RectTransform));
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.SetParent(parent, false);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -topOffset);
            rowRect.sizeDelta = new Vector2(0f, FoodDetailRowHeight);

            Text labelText = HudUiFactory.CreateText("Label", rowRect, font, 10, TextAnchor.MiddleLeft);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(0f, 1f);
            labelText.rectTransform.pivot = new Vector2(0f, 0.5f);
            labelText.rectTransform.anchoredPosition = new Vector2(2f, 0f);
            labelText.rectTransform.sizeDelta = new Vector2(58f, 0f);
            labelText.text = label;
            labelText.color = new Color(0.82f, 0.88f, 0.96f, 1f);

            GameObject barObject = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.SetParent(rowRect, false);
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.offsetMin = new Vector2(62f, 3f);
            barRect.offsetMax = new Vector2(-42f, -3f);

            Image barImage = barObject.GetComponent<Image>();
            barImage.color = barBackgroundColor;
            barImage.raycastTarget = false;

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(barRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.36f, 1f, 0.42f, 1f);
            fillImage.raycastTarget = false;

            valueText = HudUiFactory.CreateText("Value", rowRect, font, 10, TextAnchor.MiddleRight);
            valueText.rectTransform.anchorMin = new Vector2(1f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
            valueText.rectTransform.pivot = new Vector2(1f, 0.5f);
            valueText.rectTransform.anchoredPosition = new Vector2(-2f, 0f);
            valueText.rectTransform.sizeDelta = new Vector2(40f, 0f);
            valueText.color = new Color(0.86f, 0.92f, 1f, 1f);
        }

    }
}
