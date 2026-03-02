using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class LobbyHubRuntime
    {
        private void EnsureUi()
        {
            if (_uiRoot != null)
            {
                SetView(_currentView);
                RefreshMeta();
                return;
            }

            UiEventSystemBootstrap.EnsureNow();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _uiRoot = new GameObject("LobbyHubUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = _uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            RectTransform root = _uiRoot.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            EnsureUiSkins();

            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.SetParent(root, false);
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.04f, 0.06f, 0.1f, 0.58f);
            backdropImage.raycastTarget = false;

            _mainPanel = CreatePanel(root, "MainPanel", Vector2.zero, new Vector2(1280f, 780f));
            RectTransform mainRect = _mainPanel.GetComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.5f, 0.5f);
            mainRect.anchorMax = new Vector2(0.5f, 0.5f);
            mainRect.pivot = new Vector2(0.5f, 0.5f);
            mainRect.anchoredPosition = new Vector2(0f, -16f);

            _mainTitleText = CreateText(
                mainRect,
                font,
                46,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -30f),
                new Vector2(0f, 64f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Color(0.08f, 0.08f, 0.1f, 1f),
                "LOBBY");

            _metaText = CreateText(
                mainRect,
                font,
                21,
                TextAnchor.UpperLeft,
                new Vector2(26f, -84f),
                new Vector2(740f, 86f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.07f, 0.07f, 0.09f, 0.98f));

            _homeNavButton = CreateButton(mainRect, font, "HOME", new Vector2(-220f, -152f), new Vector2(200f, 48f));
            _garageNavButton = CreateButton(mainRect, font, "GARAGE", new Vector2(0f, -152f), new Vector2(200f, 48f));
            _regionNavButton = CreateButton(mainRect, font, "REGION", new Vector2(220f, -152f), new Vector2(200f, 48f));
            _homeNavButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                SetView(LobbyViewMode.Home);
            });
            _garageNavButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                SetView(LobbyViewMode.Garage);
            });
            _regionNavButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                SetView(LobbyViewMode.Region);
            });

            GameObject contentRootGo = new GameObject("ContentRoot", typeof(RectTransform));
            RectTransform contentRoot = contentRootGo.GetComponent<RectTransform>();
            contentRoot.SetParent(mainRect, false);
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = new Vector2(24f, 24f);
            contentRoot.offsetMax = new Vector2(-24f, -212f);

            _homePanel = BuildHomePanel(contentRoot, font);
            _garagePanel = BuildGaragePanel(contentRoot, font);
            _regionPanel = BuildRegionPanel(contentRoot, font);

            _promptText = CreateText(
                root,
                font,
                24,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 34f),
                new Vector2(1120f, 42f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Color(0.97f, 0.9f, 0.28f, 1f));

            if (string.IsNullOrEmpty(_inspectedRegionId) && _meta != null)
            {
                _inspectedRegionId = _meta.SelectedRegionId;
            }

            SetView(_currentView);
            RefreshPrompt();
        }

        private GameObject BuildHomePanel(RectTransform root, Font font)
        {
            GameObject panel = CreateContentPanel(root, "HomePanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();

            CreateText(
                panelRect,
                font,
                20,
                TextAnchor.MiddleLeft,
                new Vector2(0f, -18f),
                new Vector2(680f, 34f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.1f, 0.12f, 0.16f, 1f),
                "Ready for next delivery run?");

            _homeSummaryText = CreateText(
                panelRect,
                font,
                24,
                TextAnchor.UpperLeft,
                new Vector2(0f, -64f),
                new Vector2(1120f, 250f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.05f, 0.05f, 0.07f, 1f));

            _homeStartButton = CreateButton(panelRect, font, "START RUN", new Vector2(0f, 74f), new Vector2(320f, 56f), true);
            _homeStartButton.onClick.AddListener(OnStartRunClicked);

            Button exitButton = CreateButton(panelRect, font, "EXIT GAME", new Vector2(0f, 8f), new Vector2(320f, 50f));
            exitButton.onClick.AddListener(OnExitClicked);
            return panel;
        }

        private GameObject BuildGaragePanel(RectTransform root, Font font)
        {
            GameObject panel = CreateContentPanel(root, "GaragePanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();

            CreateText(
                panelRect,
                font,
                21,
                TextAnchor.UpperLeft,
                new Vector2(0f, -16f),
                new Vector2(1120f, 40f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.07f, 0.08f, 0.1f, 1f),
                "Upgrade bike performance and run rewards.");

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                float y = -72f - (i * 62f);
                _upgradeRows[i] = CreateText(
                    panelRect,
                    font,
                    21,
                    TextAnchor.MiddleLeft,
                    new Vector2(0f, y),
                    new Vector2(760f, 48f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    Color.black);

                Button button = CreateButton(
                    panelRect,
                    font,
                    "UPGRADE",
                    new Vector2(-8f, y),
                    new Vector2(210f, 46f),
                    true);
                RectTransform br = button.GetComponent<RectTransform>();
                br.anchorMin = new Vector2(1f, 1f);
                br.anchorMax = new Vector2(1f, 1f);
                br.pivot = new Vector2(1f, 1f);
                int captured = i;
                button.onClick.AddListener(() => OnUpgradeClicked(captured));
                AddButtonHoverEvents(button, () => OnUpgradeHoverEnter(captured), OnUpgradeHoverExit);
                _upgradeButtons[i] = button;
            }

            _upgradeHoverText = CreateText(
                panelRect,
                font,
                19,
                TextAnchor.UpperLeft,
                new Vector2(0f, 20f),
                new Vector2(1120f, 58f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Color(0.14f, 0.15f, 0.18f, 1f),
                "Hover an UPGRADE button to preview Lv change and real stat values.");
            return panel;
        }

        private GameObject BuildRegionPanel(RectTransform root, Font font)
        {
            GameObject panel = CreateContentPanel(root, "RegionPanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();

            CreateText(
                panelRect,
                font,
                20,
                TextAnchor.UpperLeft,
                new Vector2(0f, -16f),
                new Vector2(1120f, 32f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.08f, 0.09f, 0.11f, 1f),
                "Hover/Click a region to inspect unlock conditions.");

            _regionText = CreateText(
                panelRect,
                font,
                18,
                TextAnchor.UpperLeft,
                new Vector2(0f, -52f),
                new Vector2(340f, 40f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.1f, 0.11f, 0.13f, 1f),
                "Region List");

            for (int i = 0; i < RegionIds.Length; i++)
            {
                float y = -92f - (i * 58f);
                string regionId = RegionIds[i];
                Button itemButton = CreateButton(panelRect, font, regionId, new Vector2(0f, y), new Vector2(340f, 46f));
                RectTransform itemRect = itemButton.GetComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = new Vector2(0f, 1f);
                itemRect.pivot = new Vector2(0f, 1f);

                string capturedRegionId = regionId;
                itemButton.onClick.AddListener(() => OnRegionItemClicked(capturedRegionId));
                AddButtonHoverEvents(itemButton, () => OnRegionItemHover(capturedRegionId), OnRegionItemHoverExit);

                _regionItemButtons[i] = itemButton;
                _regionItemTexts[i] = itemButton.GetComponentInChildren<Text>();
            }

            _regionDetailText = CreateText(
                panelRect,
                font,
                19,
                TextAnchor.UpperLeft,
                new Vector2(380f, -90f),
                new Vector2(740f, 390f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.07f, 0.08f, 0.1f, 1f));

            _nextRegionButton = CreateButton(panelRect, font, "NEXT REGION", new Vector2(-120f, 14f), new Vector2(230f, 50f));
            RectTransform nextRect = _nextRegionButton.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(1f, 0f);
            nextRect.anchorMax = new Vector2(1f, 0f);
            nextRect.pivot = new Vector2(1f, 0f);
            _nextRegionButton.onClick.AddListener(OnNextRegionClicked);

            _unlockButton = CreateButton(panelRect, font, "UNLOCK", new Vector2(-370f, 14f), new Vector2(230f, 50f), true);
            RectTransform unlockRect = _unlockButton.GetComponent<RectTransform>();
            unlockRect.anchorMin = new Vector2(1f, 0f);
            unlockRect.anchorMax = new Vector2(1f, 0f);
            unlockRect.pivot = new Vector2(1f, 0f);
            _unlockButton.onClick.AddListener(OnUnlockClicked);
            _unlockButtonText = _unlockButton.GetComponentInChildren<Text>();
            return panel;
        }

        private static GameObject CreateContentPanel(RectTransform root, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            return panel;
        }
    }
}
