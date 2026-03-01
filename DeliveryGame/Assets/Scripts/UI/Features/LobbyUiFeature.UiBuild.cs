using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Features
{
    internal sealed partial class LobbyUiFeature
    {
        private void EnsureLobbyModuleUi()
        {
            if (_lobbyRoot != null)
            {
                return;
            }

            UiEventSystemBootstrap.EnsureNow();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _lobbyRoot = new GameObject("LobbyUiRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _lobbyRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            CanvasScaler scaler = _lobbyRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = _lobbyRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject panelGo = new GameObject("LobbyPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.SetParent(rootRect, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -24f);
            panelRect.sizeDelta = new Vector2(720f, 430f);

            Image panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);

            Text title = LobbyUiFactory.CreateText("Title", panelRect, font, 64, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.22f, 1f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            title.rectTransform.sizeDelta = new Vector2(0f, 90f);
            title.text = "DELIVERY RUN";

            Text subtitle = LobbyUiFactory.CreateText("Subtitle", panelRect, font, 22, TextAnchor.UpperCenter, new Color(0.88f, 0.92f, 0.98f, 0.95f));
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -112f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 70f);
            subtitle.text = "Space: Accept Order  |  F: Interact";

            _lobbyMetaCashText = LobbyUiFactory.CreateText("MetaCashText", panelRect, font, 24, TextAnchor.MiddleCenter, new Color(0.98f, 0.94f, 0.62f, 1f));
            _lobbyMetaCashText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaCashText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaCashText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaCashText.rectTransform.anchoredPosition = new Vector2(0f, -156f);
            _lobbyMetaCashText.rectTransform.sizeDelta = new Vector2(0f, 36f);

            _lobbyMetaRegionText = LobbyUiFactory.CreateText("MetaRegionText", panelRect, font, 18, TextAnchor.MiddleCenter, new Color(0.85f, 0.92f, 1f, 0.98f));
            _lobbyMetaRegionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaRegionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaRegionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaRegionText.rectTransform.anchoredPosition = new Vector2(0f, -188f);
            _lobbyMetaRegionText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            _lobbyMetaSelectedRegionText = LobbyUiFactory.CreateText("MetaSelectedRegionText", panelRect, font, 18, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f, 0.98f));
            _lobbyMetaSelectedRegionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaSelectedRegionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaSelectedRegionText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaSelectedRegionText.rectTransform.anchoredPosition = new Vector2(0f, -214f);
            _lobbyMetaSelectedRegionText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            _lobbyMetaUpgradeText = LobbyUiFactory.CreateText("MetaUpgradeText", panelRect, font, 17, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f, 0.92f));
            _lobbyMetaUpgradeText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaUpgradeText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaUpgradeText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaUpgradeText.rectTransform.anchoredPosition = new Vector2(0f, -240f);
            _lobbyMetaUpgradeText.rectTransform.sizeDelta = new Vector2(0f, 48f);

            _lobbyMetaUnlockText = LobbyUiFactory.CreateText("MetaUnlockText", panelRect, font, 16, TextAnchor.UpperCenter, new Color(0.94f, 0.96f, 1f, 0.95f));
            _lobbyMetaUnlockText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _lobbyMetaUnlockText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _lobbyMetaUnlockText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _lobbyMetaUnlockText.rectTransform.anchoredPosition = new Vector2(0f, -286f);
            _lobbyMetaUnlockText.rectTransform.sizeDelta = new Vector2(0f, 58f);
            _lobbyMetaUnlockText.lineSpacing = 1.04f;

            Button startButton = LobbyUiFactory.CreateButton("StartRunButton", panelRect, font, "START RUN");
            RectTransform startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 158f);
            startRect.sizeDelta = new Vector2(250f, 58f);
            startButton.onClick.AddListener(OnLobbyStartRunClicked);

            Button settingsButton = LobbyUiFactory.CreateButton("SettingsButton", panelRect, font, "SETTINGS");
            RectTransform settingsRect = settingsButton.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0.5f, 0f);
            settingsRect.anchorMax = new Vector2(0.5f, 0f);
            settingsRect.pivot = new Vector2(0.5f, 0f);
            settingsRect.anchoredPosition = new Vector2(0f, 92f);
            settingsRect.sizeDelta = new Vector2(250f, 52f);
            settingsButton.onClick.AddListener(OnLobbySettingsClicked);

            Button sectorButton = LobbyUiFactory.CreateButton("SectorButton", panelRect, font, "CHANGE SECTOR");
            RectTransform sectorRect = sectorButton.GetComponent<RectTransform>();
            sectorRect.anchorMin = new Vector2(0.5f, 0f);
            sectorRect.anchorMax = new Vector2(0.5f, 0f);
            sectorRect.pivot = new Vector2(0.5f, 0f);
            sectorRect.anchoredPosition = new Vector2(0f, 34f);
            sectorRect.sizeDelta = new Vector2(250f, 48f);
            sectorButton.onClick.AddListener(OnLobbyChangeSectorClicked);

            _lobbyUnlockRegionButton = LobbyUiFactory.CreateButton("UnlockRegionButton", panelRect, font, "UNLOCK REGION");
            RectTransform unlockRect = _lobbyUnlockRegionButton.GetComponent<RectTransform>();
            unlockRect.anchorMin = new Vector2(0.5f, 0f);
            unlockRect.anchorMax = new Vector2(0.5f, 0f);
            unlockRect.pivot = new Vector2(0.5f, 0f);
            unlockRect.anchoredPosition = new Vector2(0f, -20f);
            unlockRect.sizeDelta = new Vector2(250f, 44f);
            _lobbyUnlockRegionButton.onClick.AddListener(OnLobbyUnlockRegionClicked);
            _lobbyUnlockRegionButtonLabel = _lobbyUnlockRegionButton.GetComponentInChildren<Text>();

            Button exitButton = LobbyUiFactory.CreateButton("ExitButton", panelRect, font, "EXIT");
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            exitRect.anchorMin = new Vector2(0.5f, 0f);
            exitRect.anchorMax = new Vector2(0.5f, 0f);
            exitRect.pivot = new Vector2(0.5f, 0f);
            exitRect.anchoredPosition = new Vector2(0f, -72f);
            exitRect.sizeDelta = new Vector2(250f, 44f);
            exitButton.onClick.AddListener(OnLobbyExitClicked);

            BuildLobbySettingsPanel(panelRect, font);
            RefreshLobbyMetaTexts();
        }

        private void BuildLobbySettingsPanel(RectTransform parent, Font font)
        {
            GameObject panelGo = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 2f);
            panelRect.sizeDelta = new Vector2(560f, 220f);

            Image panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.14f, 0.2f, 0.97f);
            panelImage.raycastTarget = true;

            Text header = LobbyUiFactory.CreateText("Header", panelRect, font, 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.28f, 1f));
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            header.rectTransform.sizeDelta = new Vector2(0f, 40f);
            header.text = "SETTINGS";

            Text bgmLabel = LobbyUiFactory.CreateText("BgmLabel", panelRect, font, 18, TextAnchor.MiddleLeft, new Color(0.93f, 0.96f, 1f, 1f));
            bgmLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            bgmLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            bgmLabel.rectTransform.pivot = new Vector2(0f, 1f);
            bgmLabel.rectTransform.anchoredPosition = new Vector2(26f, -66f);
            bgmLabel.rectTransform.sizeDelta = new Vector2(130f, 26f);
            bgmLabel.text = "BGM";

            _lobbyBgmSlider = LobbyUiFactory.CreateSlider("BgmSlider", panelRect, new Vector2(170f, -70f), new Vector2(350f, 20f));
            _lobbyBgmSlider.onValueChanged.AddListener(OnLobbyBgmSliderChanged);

            Text uiLabel = LobbyUiFactory.CreateText("UiLabel", panelRect, font, 18, TextAnchor.MiddleLeft, new Color(0.93f, 0.96f, 1f, 1f));
            uiLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            uiLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            uiLabel.rectTransform.pivot = new Vector2(0f, 1f);
            uiLabel.rectTransform.anchoredPosition = new Vector2(26f, -116f);
            uiLabel.rectTransform.sizeDelta = new Vector2(130f, 26f);
            uiLabel.text = "UI SFX";

            _lobbyUiSlider = LobbyUiFactory.CreateSlider("UiSlider", panelRect, new Vector2(170f, -120f), new Vector2(350f, 20f));
            _lobbyUiSlider.onValueChanged.AddListener(OnLobbyUiSliderChanged);

            Button closeButton = LobbyUiFactory.CreateButton("CloseSettings", panelRect, font, "CLOSE");
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 18f);
            closeRect.sizeDelta = new Vector2(190f, 42f);
            closeButton.onClick.AddListener(() =>
            {
                TryPlayLobbyUiClick();
                SetLobbySettingsVisible(false);
            });

            _lobbySettingsPanel = panelGo;
            SetLobbySettingsVisible(false);

            if (_lobbyAudioManager == null)
            {
                Services.TryGet(out _lobbyAudioManager);
            }

            if (_lobbyAudioManager != null)
            {
                _lobbyBgmSlider.SetValueWithoutNotify(_lobbyAudioManager.GetBgmVolume01());
                _lobbyUiSlider.SetValueWithoutNotify(_lobbyAudioManager.GetUiVolume01());
            }
            else
            {
                _lobbyBgmSlider.SetValueWithoutNotify(0.65f);
                _lobbyUiSlider.SetValueWithoutNotify(0.9f);
            }
        }
    }
}
