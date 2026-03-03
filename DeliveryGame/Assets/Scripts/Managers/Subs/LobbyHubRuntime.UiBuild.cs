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
            Font font = ResolveLobbyFont();
            _uiRoot = new GameObject("LobbyHubUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            _canvasScaler = _uiRoot.GetComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasScaler.matchWidthOrHeight = 0.5f;

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
            backdropImage.color = new Color(0.03f, 0.06f, 0.1f, 0.62f);
            backdropImage.raycastTarget = false;

            _mainPanel = CreatePanel(root, "MainPanel", Vector2.zero, new Vector2(1420f, 860f));
            _mainPanelRect = _mainPanel.GetComponent<RectTransform>();
            _mainPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _mainPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _mainPanelRect.pivot = new Vector2(0.5f, 0.5f);
            _mainPanelRect.anchoredPosition = new Vector2(0f, -12f);

            GameObject headerBar = CreatePanel(_mainPanelRect, "HeaderBar", new Vector2(24f, -20f), new Vector2(1372f, 152f));
            _headerBarRect = headerBar.GetComponent<RectTransform>();
            Image headerImage = headerBar.GetComponent<Image>();
            if (_buttonAccentSkinSprite != null)
            {
                headerImage.sprite = _buttonAccentSkinSprite;
                headerImage.type = Image.Type.Sliced;
            }
            headerImage.color = new Color(1f, 1f, 1f, 0.95f);

            _mainTitleText = CreateText(
                _headerBarRect,
                font,
                58,
                TextAnchor.MiddleLeft,
                new Vector2(30f, -18f),
                new Vector2(620f, 72f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.08f, 0.09f, 0.12f, 1f),
                "LOBBY");

            Text subtitle = CreateText(
                _headerBarRect,
                font,
                20,
                TextAnchor.UpperLeft,
                new Vector2(32f, -78f),
                new Vector2(760f, 42f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.1f, 0.13f, 0.17f, 1f),
                "Inspect region unlocks, tune upgrades, then launch your next run.");
            subtitle.fontStyle = FontStyle.Normal;

            _metaText = CreateText(
                _headerBarRect,
                font,
                20,
                TextAnchor.UpperRight,
                new Vector2(-30f, -18f),
                new Vector2(520f, 112f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Color(0.08f, 0.1f, 0.14f, 1f));
            _metaText.lineSpacing = 1.07f;

            _homeNavButton = CreateButton(_mainPanelRect, font, "HOME", new Vector2(-280f, -196f), new Vector2(250f, 58f));
            _garageNavButton = CreateButton(_mainPanelRect, font, "GARAGE", new Vector2(0f, -196f), new Vector2(250f, 58f));
            _regionNavButton = CreateButton(_mainPanelRect, font, "REGION", new Vector2(280f, -196f), new Vector2(250f, 58f));
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
            _contentRootRect = contentRootGo.GetComponent<RectTransform>();
            _contentRootRect.SetParent(_mainPanelRect, false);
            _contentRootRect.anchorMin = Vector2.zero;
            _contentRootRect.anchorMax = Vector2.one;
            _contentRootRect.offsetMin = new Vector2(26f, 26f);
            _contentRootRect.offsetMax = new Vector2(-26f, -270f);

            _homePanel = BuildHomePanel(_contentRootRect, font);
            _garagePanel = BuildGaragePanel(_contentRootRect, font);
            _regionPanel = BuildRegionPanel(_contentRootRect, font);

            GameObject promptPanel = CreatePanel(root, "PromptPanel", new Vector2(0f, -26f), new Vector2(980f, 54f));
            _promptPanelRect = promptPanel.GetComponent<RectTransform>();
            _promptPanelRect.anchorMin = new Vector2(0.5f, 0f);
            _promptPanelRect.anchorMax = new Vector2(0.5f, 0f);
            _promptPanelRect.pivot = new Vector2(0.5f, 0f);
            Image promptImage = promptPanel.GetComponent<Image>();
            promptImage.color = new Color(1f, 1f, 1f, 0.92f);

            _promptText = CreateText(
                _promptPanelRect,
                font,
                22,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Color(0.08f, 0.11f, 0.16f, 1f),
                "UI Lobby Mode");
            _promptText.rectTransform.offsetMin = new Vector2(18f, 6f);
            _promptText.rectTransform.offsetMax = new Vector2(-18f, -6f);

            CreateText(
                root,
                font,
                18,
                TextAnchor.LowerCenter,
                new Vector2(0f, 28f),
                new Vector2(960f, 28f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Color(0.93f, 0.95f, 0.99f, 0.86f),
                "Press [F] near world objects to open related page quickly.");

            if (string.IsNullOrEmpty(_inspectedRegionId) && _meta != null)
            {
                _inspectedRegionId = _meta.SelectedRegionId;
            }

            SetView(_currentView);
            ApplyResponsiveLayout();
            RefreshPrompt();
        }

        private GameObject BuildHomePanel(RectTransform root, Font font)
        {
            GameObject panel = CreateContentPanel(root, "HomePanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            GameObject infoCard = CreatePanel(panelRect, "HomeInfoCard", new Vector2(22f, -22f), new Vector2(920f, 458f));
            RectTransform infoRect = infoCard.GetComponent<RectTransform>();
            Image infoImage = infoCard.GetComponent<Image>();
            infoImage.color = new Color(1f, 1f, 1f, 0.96f);

            CreateText(
                infoRect,
                font,
                24,
                TextAnchor.UpperLeft,
                new Vector2(26f, -22f),
                new Vector2(840f, 42f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.1f, 0.12f, 0.16f, 1f),
                "RUN BRIEFING");

            CreateText(
                infoRect,
                font,
                19,
                TextAnchor.MiddleLeft,
                new Vector2(28f, -74f),
                new Vector2(840f, 30f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.16f, 0.17f, 0.2f, 1f),
                "Current economy and region state before departure.");

            _homeSummaryText = CreateText(
                infoRect,
                font,
                22,
                TextAnchor.UpperLeft,
                new Vector2(28f, -116f),
                new Vector2(860f, 300f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.08f, 0.09f, 0.12f, 1f));
            _homeSummaryText.lineSpacing = 1.06f;

            GameObject actionCard = CreatePanel(panelRect, "HomeActionCard", new Vector2(968f, -22f), new Vector2(378f, 458f));
            RectTransform actionRect = actionCard.GetComponent<RectTransform>();
            Image actionImage = actionCard.GetComponent<Image>();
            actionImage.color = new Color(1f, 1f, 1f, 0.95f);

            CreateText(
                actionRect,
                font,
                24,
                TextAnchor.UpperCenter,
                new Vector2(0f, -26f),
                new Vector2(320f, 42f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Color(0.08f, 0.1f, 0.14f, 1f),
                "ACTION");

            _homeStartButton = CreateButton(actionRect, font, "START RUN", new Vector2(0f, 220f), new Vector2(296f, 68f), true);
            _homeStartButton.onClick.AddListener(OnStartRunClicked);

            Button exitButton = CreateButton(actionRect, font, "EXIT GAME", new Vector2(0f, 138f), new Vector2(296f, 58f));
            exitButton.onClick.AddListener(OnExitClicked);

            CreateText(
                actionRect,
                font,
                18,
                TextAnchor.UpperCenter,
                new Vector2(0f, 88f),
                new Vector2(322f, 120f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Color(0.2f, 0.24f, 0.3f, 1f),
                "Launch run when region and upgrade setup is ready.");
            return panel;
        }

        private GameObject BuildGaragePanel(RectTransform root, Font font)
        {
            GameObject panel = CreateContentPanel(root, "GaragePanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            GameObject listCard = CreatePanel(panelRect, "UpgradeListCard", new Vector2(22f, -22f), new Vector2(1324f, 458f));
            RectTransform listRect = listCard.GetComponent<RectTransform>();
            Image listImage = listCard.GetComponent<Image>();
            listImage.color = new Color(1f, 1f, 1f, 0.96f);

            CreateText(
                listRect,
                font,
                24,
                TextAnchor.UpperLeft,
                new Vector2(26f, -20f),
                new Vector2(1160f, 40f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.08f, 0.1f, 0.14f, 1f),
                "UPGRADE GARAGE");

            CreateText(
                listRect,
                font,
                19,
                TextAnchor.UpperLeft,
                new Vector2(26f, -58f),
                new Vector2(1120f, 30f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.16f, 0.17f, 0.2f, 1f),
                "Upgrade bike performance and run rewards.");

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                float y = -106f - (i * 66f);
                GameObject rowCard = CreatePanel(listRect, "UpgradeRow_" + i, new Vector2(22f, y), new Vector2(1278f, 56f));
                RectTransform rowRect = rowCard.GetComponent<RectTransform>();
                Image rowImage = rowCard.GetComponent<Image>();
                rowImage.color = new Color(1f, 1f, 1f, 0.93f);
                _upgradeRowImages[i] = rowImage;

                _upgradeRows[i] = CreateText(
                    rowRect,
                    font,
                    20,
                    TextAnchor.MiddleLeft,
                    new Vector2(22f, -10f),
                    new Vector2(880f, 36f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    Color.black);

                Button button = CreateButton(
                    rowRect,
                    font,
                    "UPGRADE",
                    new Vector2(-20f, 5f),
                    new Vector2(260f, 46f),
                    true);
                RectTransform br = button.GetComponent<RectTransform>();
                br.anchorMin = new Vector2(1f, 1f);
                br.anchorMax = new Vector2(1f, 1f);
                br.pivot = new Vector2(1f, 1f);
                int captured = i;
                button.onClick.AddListener(() => OnUpgradeClicked(captured));
                AddButtonHoverEvents(button, () => OnUpgradeHoverEnter(captured), OnUpgradeHoverExit);
                _upgradeButtons[i] = button;

                _upgradeStateTexts[i] = CreateText(
                    rowRect,
                    font,
                    17,
                    TextAnchor.MiddleRight,
                    new Vector2(-296f, -10f),
                    new Vector2(232f, 36f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Color(0.1f, 0.12f, 0.16f, 1f));
                _upgradeStateTexts[i].fontStyle = FontStyle.Bold;
            }

            _upgradeHoverText = CreateText(
                listRect,
                font,
                19,
                TextAnchor.UpperLeft,
                new Vector2(24f, 28f),
                new Vector2(1220f, 88f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Color(0.1f, 0.12f, 0.16f, 1f),
                "Hover an UPGRADE button to preview Lv change and real stat values.");
            _upgradeHoverText.lineSpacing = 1.05f;
            return panel;
        }

        private GameObject BuildRegionPanel(RectTransform root, Font font)
        {
            GameObject panel = CreateContentPanel(root, "RegionPanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            GameObject leftCard = CreatePanel(panelRect, "RegionListCard", new Vector2(22f, -22f), new Vector2(418f, 458f));
            RectTransform leftRect = leftCard.GetComponent<RectTransform>();
            Image leftImage = leftCard.GetComponent<Image>();
            leftImage.color = new Color(1f, 1f, 1f, 0.96f);

            GameObject rightCard = CreatePanel(panelRect, "RegionDetailCard", new Vector2(462f, -22f), new Vector2(884f, 458f));
            RectTransform rightRect = rightCard.GetComponent<RectTransform>();
            Image rightImage = rightCard.GetComponent<Image>();
            rightImage.color = new Color(1f, 1f, 1f, 0.96f);

            CreateText(
                leftRect,
                font,
                23,
                TextAnchor.UpperLeft,
                new Vector2(20f, -18f),
                new Vector2(360f, 34f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.08f, 0.1f, 0.14f, 1f),
                "REGIONS");

            _regionText = CreateText(
                leftRect,
                font,
                18,
                TextAnchor.UpperLeft,
                new Vector2(20f, -52f),
                new Vector2(360f, 34f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.1f, 0.11f, 0.13f, 1f),
                "Region List");

            for (int i = 0; i < RegionIds.Length; i++)
            {
                float y = -94f - (i * 50f);
                string regionId = RegionIds[i];
                Button itemButton = CreateButton(leftRect, font, regionId, new Vector2(20f, y), new Vector2(374f, 42f));
                RectTransform itemRect = itemButton.GetComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = new Vector2(0f, 1f);
                itemRect.pivot = new Vector2(0f, 1f);

                string capturedRegionId = regionId;
                itemButton.onClick.AddListener(() => OnRegionItemClicked(capturedRegionId));
                AddButtonHoverEvents(itemButton, () => OnRegionItemHover(capturedRegionId), OnRegionItemHoverExit);

                _regionItemButtons[i] = itemButton;
                Text label = itemButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.MiddleLeft;
                    label.rectTransform.offsetMin = new Vector2(44f, 0f);
                    label.rectTransform.offsetMax = new Vector2(-128f, 0f);
                    label.fontSize = 17;
                }

                _regionItemTexts[i] = label;

                GameObject iconGo = new GameObject("StateIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.SetParent(itemRect, false);
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(14f, 0f);
                iconRect.sizeDelta = new Vector2(20f, 20f);
                Image iconImage = iconGo.GetComponent<Image>();
                iconImage.raycastTarget = false;
                _regionItemIcons[i] = iconImage;

                Text stateText = CreateText(
                    itemRect,
                    font,
                    15,
                    TextAnchor.MiddleRight,
                    new Vector2(-12f, 0f),
                    new Vector2(112f, 30f),
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Color(0.1f, 0.12f, 0.16f, 1f));
                stateText.fontStyle = FontStyle.Bold;
                _regionItemStateTexts[i] = stateText;
            }

            CreateText(
                rightRect,
                font,
                23,
                TextAnchor.UpperLeft,
                new Vector2(24f, -18f),
                new Vector2(760f, 34f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.08f, 0.1f, 0.14f, 1f),
                "UNLOCK DETAILS");

            _regionDetailText = CreateText(
                rightRect,
                font,
                19,
                TextAnchor.UpperLeft,
                new Vector2(24f, -60f),
                new Vector2(836f, 320f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.07f, 0.08f, 0.1f, 1f));
            _regionDetailText.lineSpacing = 1.04f;

            _nextRegionButton = CreateButton(rightRect, font, "NEXT REGION", new Vector2(-282f, 16f), new Vector2(250f, 54f));
            RectTransform nextRect = _nextRegionButton.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(1f, 0f);
            nextRect.anchorMax = new Vector2(1f, 0f);
            nextRect.pivot = new Vector2(1f, 0f);
            _nextRegionButton.onClick.AddListener(OnNextRegionClicked);

            _unlockButton = CreateButton(rightRect, font, "UNLOCK", new Vector2(-24f, 16f), new Vector2(250f, 54f), true);
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
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(root, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.23f, 0.12f);
            image.raycastTarget = false;
            return panel;
        }

        private void UpdateResponsiveLayoutIfNeeded()
        {
            if (_uiRoot == null)
            {
                return;
            }

            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight)
            {
                return;
            }

            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            if (_mainPanelRect == null)
            {
                return;
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : (16f / 9f);
            bool compact = aspect <= 1.45f;
            bool ultraWide = aspect >= 2.0f;

            if (_canvasScaler != null)
            {
                _canvasScaler.matchWidthOrHeight = compact
                    ? 1f
                    : (ultraWide ? 0.28f : 0.5f);
            }

            Vector2 mainSize = new Vector2(1420f, 860f);
            Vector2 mainPosition = new Vector2(0f, -12f);
            Vector2 headerSize = new Vector2(1372f, 152f);
            Vector2 headerPosition = new Vector2(24f, -20f);
            Vector2 contentOffsetMin = new Vector2(26f, 26f);
            Vector2 contentOffsetMax = new Vector2(-26f, -270f);
            Vector2 promptSize = new Vector2(980f, 54f);
            Vector2 promptPosition = new Vector2(0f, -26f);
            float navY = -196f;
            float navSpacing = 280f;
            float navWidth = 250f;

            if (compact)
            {
                mainSize = new Vector2(1420f, 920f);
                mainPosition = new Vector2(0f, -4f);
                headerSize = new Vector2(1372f, 146f);
                headerPosition = new Vector2(24f, -18f);
                contentOffsetMin = new Vector2(24f, 22f);
                contentOffsetMax = new Vector2(-24f, -258f);
                promptSize = new Vector2(1060f, 58f);
                promptPosition = new Vector2(0f, -18f);
                navY = -208f;
                navSpacing = 262f;
                navWidth = 232f;
            }
            else if (ultraWide)
            {
                mainSize = new Vector2(1560f, 860f);
                mainPosition = new Vector2(0f, -14f);
                headerSize = new Vector2(1512f, 150f);
                headerPosition = new Vector2(24f, -20f);
                contentOffsetMin = new Vector2(26f, 26f);
                contentOffsetMax = new Vector2(-26f, -262f);
                promptSize = new Vector2(1120f, 54f);
                promptPosition = new Vector2(0f, -24f);
                navY = -194f;
                navSpacing = 298f;
                navWidth = 266f;
            }

            _mainPanelRect.sizeDelta = mainSize;
            _mainPanelRect.anchoredPosition = mainPosition;

            if (_headerBarRect != null)
            {
                _headerBarRect.sizeDelta = headerSize;
                _headerBarRect.anchoredPosition = headerPosition;
            }

            if (_contentRootRect != null)
            {
                _contentRootRect.offsetMin = contentOffsetMin;
                _contentRootRect.offsetMax = contentOffsetMax;
            }

            if (_promptPanelRect != null)
            {
                _promptPanelRect.sizeDelta = promptSize;
                _promptPanelRect.anchoredPosition = promptPosition;
            }

            SetNavButtonRect(_homeNavButton, -navSpacing, navY, navWidth);
            SetNavButtonRect(_garageNavButton, 0f, navY, navWidth);
            SetNavButtonRect(_regionNavButton, navSpacing, navY, navWidth);

            if (_mainTitleText != null)
            {
                _mainTitleText.fontSize = compact ? 54 : 58;
            }

            if (_metaText != null)
            {
                _metaText.fontSize = compact ? 18 : 20;
                _metaText.rectTransform.sizeDelta = compact
                    ? new Vector2(500f, 104f)
                    : new Vector2(520f, 112f);
            }

            if (_promptText != null)
            {
                _promptText.fontSize = compact ? 21 : 22;
            }
        }

        private static void SetNavButtonRect(Button button, float x, float y, float width)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private Font ResolveLobbyFont()
        {
            if (_catalog != null && _catalog.LobbyPrimaryFont != null)
            {
                return _catalog.LobbyPrimaryFont;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
