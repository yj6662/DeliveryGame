using System.Text;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class StartMenuRuntime
    {
        private const float ScenePollInterval = 0.25f;
        private const int SaveSlotCount = 3;

        private readonly ServiceRegistry _services;
        private readonly EventBus _events;

        private MetaProgressionService _meta;
        private AudioManager _audio;
        private UiPrefabCatalogSO _catalog;

        private bool _isStartScene;
        private float _scenePollElapsed;

        private GameObject _uiRoot;
        private Text _slotSummaryText;
        private GameObject _slotSelectPanel;
        private Text _slotSelectHeaderText;
        private Text _settingsPanelText;
        private GameObject _settingsPanel;
        private readonly Button[] _slotButtons = new Button[SaveSlotCount];
        private readonly Text[] _slotButtonLabels = new Text[SaveSlotCount];

        internal StartMenuRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal void Initialize()
        {
            _services.TryGet(out _meta);
            _services.TryGet(out _audio);
            _catalog = UiPrefabCatalogLoader.LoadOrNull();

            HandleScene(SceneManager.GetActiveScene().name);
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(ref _scenePollElapsed, ScenePollInterval, unscaledDeltaTime))
            {
                HandleScene(SceneManager.GetActiveScene().name);
            }

            if (!_isStartScene)
            {
                return;
            }

            EnsureRefs();
            EnsureUi();
        }

        internal void Shutdown()
        {
            _isStartScene = false;
            DestroyUi();
        }

        internal void OnTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.StartScene)
            {
                return;
            }

            _isStartScene = false;
            DestroyUi();
        }

        internal void OnTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleScene(evt.SceneName);
        }

        internal void OnMetaDirty()
        {
            RefreshMetaSummary();
        }

        private void HandleScene(string sceneName)
        {
            bool shouldShow = sceneName == SceneNames.StartScene;
            if (_isStartScene == shouldShow)
            {
                if (_isStartScene)
                {
                    EnsureRefs();
                    EnsureUi();
                }

                return;
            }

            _isStartScene = shouldShow;
            if (_isStartScene)
            {
                EnsureRefs();
                EnsureUi();
                return;
            }

            DestroyUi();
        }

        private void EnsureRefs()
        {
            if (_meta == null)
            {
                _services.TryGet(out _meta);
            }

            if (_audio == null)
            {
                _services.TryGet(out _audio);
            }
        }

        private void EnsureUi()
        {
            if (_uiRoot != null)
            {
                RefreshMetaSummary();
                return;
            }

            UiEventSystemBootstrap.EnsureNow();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                Debug.LogError("[StartMenuRuntime] LegacyRuntime.ttf builtin font not found.");
                return;
            }

            _uiRoot = new GameObject("StartMenuUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;

            CanvasScaler scaler = _uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = _uiRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image backdrop = CreatePanelImage(rootRect, "Backdrop");
            RectTransform backdropRect = backdrop.rectTransform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdrop.color = new Color(0.04f, 0.06f, 0.1f, 0.72f);
            backdrop.raycastTarget = false;

            Image panel = CreatePanelImage(rootRect, "MainPanel");
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -10f);
            panelRect.sizeDelta = new Vector2(860f, 720f);
            panel.color = new Color(0.96f, 0.97f, 0.99f, 0.98f);

            CreateText(
                panelRect,
                font,
                "DELIVERY RUN",
                52,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -56f),
                new Vector2(640f, 72f),
                new Color(0.05f, 0.06f, 0.08f, 1f));

            CreateText(
                panelRect,
                font,
                "Select an option",
                24,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -118f),
                new Vector2(560f, 38f),
                new Color(0.09f, 0.1f, 0.12f, 1f));

            Button startButton = CreateButton(panelRect, font, "START GAME", new Vector2(0f, 194f), new Vector2(380f, 68f), true);
            Button settingsButton = CreateButton(panelRect, font, "SETTINGS", new Vector2(0f, 112f), new Vector2(380f, 64f), false);
            Button exitButton = CreateButton(panelRect, font, "EXIT", new Vector2(0f, 34f), new Vector2(380f, 64f), false);

            startButton.onClick.AddListener(OnStartGameClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
            exitButton.onClick.AddListener(OnExitClicked);

            _slotSummaryText = CreateText(
                panelRect,
                font,
                string.Empty,
                18,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(-390f, 256f),
                new Vector2(780f, 220f),
                new Color(0.06f, 0.07f, 0.09f, 1f));

            BuildSaveSlotPanel(panelRect, font);
            BuildSettingsPanel(panelRect, font);

            _slotSelectPanel.SetActive(false);
            _settingsPanel.SetActive(false);

            RefreshMetaSummary();
        }

        private void BuildSaveSlotPanel(RectTransform parent, Font font)
        {
            Image slotPanelImage = CreatePanelImage(parent, "SaveSlotPanel");
            _slotSelectPanel = slotPanelImage.gameObject;

            RectTransform slotRect = slotPanelImage.rectTransform;
            slotRect.anchorMin = new Vector2(0.5f, 0f);
            slotRect.anchorMax = new Vector2(0.5f, 0f);
            slotRect.pivot = new Vector2(0.5f, 0f);
            slotRect.anchoredPosition = new Vector2(0f, 26f);
            slotRect.sizeDelta = new Vector2(700f, 360f);
            slotPanelImage.color = new Color(0.9f, 0.92f, 0.96f, 0.97f);

            _slotSelectHeaderText = CreateText(
                slotRect,
                font,
                "SELECT SAVE SLOT",
                22,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -28f),
                new Vector2(620f, 52f),
                new Color(0.06f, 0.07f, 0.1f, 1f));

            for (int i = 0; i < SaveSlotCount; i++)
            {
                float y = 232f - (i * 86f);
                Button slotButton = CreateButton(
                    slotRect,
                    font,
                    string.Empty,
                    new Vector2(0f, y),
                    new Vector2(620f, 72f),
                    false);

                Text label = slotButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.MiddleLeft;
                    label.fontSize = 18;
                    label.rectTransform.offsetMin = new Vector2(18f, 0f);
                    label.rectTransform.offsetMax = new Vector2(-18f, 0f);
                }

                int capturedSlotIndex = i;
                slotButton.onClick.AddListener(() => OnSaveSlotClicked(capturedSlotIndex));
                _slotButtons[i] = slotButton;
                _slotButtonLabels[i] = label;
            }

            Button closeButton = CreateButton(slotRect, font, "CLOSE", new Vector2(0f, 20f), new Vector2(220f, 50f), false);
            closeButton.onClick.AddListener(() =>
            {
                PlayUiClick();
                SetSaveSlotPanelVisible(false);
            });
        }

        private void BuildSettingsPanel(RectTransform parent, Font font)
        {
            Image settingsPanelImage = CreatePanelImage(parent, "SettingsPanel");
            _settingsPanel = settingsPanelImage.gameObject;

            RectTransform settingsRect = settingsPanelImage.rectTransform;
            settingsRect.anchorMin = new Vector2(0.5f, 0f);
            settingsRect.anchorMax = new Vector2(0.5f, 0f);
            settingsRect.pivot = new Vector2(0.5f, 0f);
            settingsRect.anchoredPosition = new Vector2(0f, 26f);
            settingsRect.sizeDelta = new Vector2(560f, 170f);
            settingsPanelImage.color = new Color(0.9f, 0.92f, 0.96f, 0.95f);

            _settingsPanelText = CreateText(
                settingsRect,
                font,
                "Settings placeholder\nAudio/graphics options can be connected here.",
                17,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.07f, 0.08f, 0.1f, 1f));
            _settingsPanelText.rectTransform.offsetMin = new Vector2(12f, 12f);
            _settingsPanelText.rectTransform.offsetMax = new Vector2(-12f, -12f);
        }

        private void DestroyUi()
        {
            _slotSummaryText = null;
            _slotSelectHeaderText = null;
            _settingsPanelText = null;
            _slotSelectPanel = null;
            _settingsPanel = null;

            for (int i = 0; i < SaveSlotCount; i++)
            {
                _slotButtons[i] = null;
                _slotButtonLabels[i] = null;
            }

            if (_uiRoot != null)
            {
                Object.Destroy(_uiRoot);
                _uiRoot = null;
            }
        }

        private void OnStartGameClicked()
        {
            PlayUiClick();
            SetSettingsPanelVisible(false);
            SetSaveSlotPanelVisible(true);
            RefreshMetaSummary();
        }

        private void OnSaveSlotClicked(int slotIndex)
        {
            PlayUiClick();
            EnsureRefs();
            if (_meta == null || !_meta.IsValidSaveSlot(slotIndex))
            {
                return;
            }

            _events.Publish(new MetaSaveSlotLoadRequested { SlotIndex = slotIndex });
            _events.Publish(new BootToLobbyRequested());
        }

        private void OnSettingsClicked()
        {
            PlayUiClick();
            bool nextVisible = _settingsPanel == null || !_settingsPanel.activeSelf;
            SetSaveSlotPanelVisible(false);
            SetSettingsPanelVisible(nextVisible);
        }

        private void OnExitClicked()
        {
            PlayUiClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RefreshMetaSummary()
        {
            if (_slotSummaryText == null)
            {
                return;
            }

            EnsureRefs();
            if (_meta == null)
            {
                _slotSummaryText.text =
                    "SAVE SLOTS\nMeta progression unavailable.\n\n" +
                    "Press START GAME to open slot selection.";
                RefreshSaveSlotPanel();
                return;
            }

            int activeSlot = _meta.ActiveSaveSlotIndex;
            MetaProgressionService.SaveSlotSummary activeSummary;
            bool hasActiveSummary = _meta.TryGetSlotSummary(activeSlot, out activeSummary);

            var builder = new StringBuilder(220);
            builder.Append("CURRENT SLOT\n");
            if (hasActiveSummary && activeSummary.HasData)
            {
                builder.Append("Slot ");
                builder.Append(activeSlot + 1);
                builder.Append(" [ACTIVE]\n");
                builder.Append("$");
                builder.Append(activeSummary.TotalCash);
                builder.Append(" / ");
                builder.Append(LobbyRegionMetaUtil.FormatRegionName(activeSummary.SelectedRegionId));
            }
            else
            {
                builder.Append("Slot ");
                builder.Append(activeSlot + 1);
                builder.Append(" [ACTIVE]\n");
                builder.Append("EMPTY");
            }

            builder.Append("\n\nPress START GAME to open slot selection.");
            _slotSummaryText.text = builder.ToString();

            RefreshSaveSlotPanel();
        }

        private void RefreshSaveSlotPanel()
        {
            if (_slotSelectHeaderText != null)
            {
                _slotSelectHeaderText.text = "SELECT SAVE SLOT  (Empty: New Game / Saved: Continue)";
            }

            EnsureRefs();
            for (int i = 0; i < SaveSlotCount; i++)
            {
                Button slotButton = _slotButtons[i];
                Text label = _slotButtonLabels[i];
                if (slotButton == null || label == null)
                {
                    continue;
                }

                bool hasData = false;
                int totalCash = 0;
                string regionId = MetaProgressionConstants.RegionIds[0];
                bool isActive = false;

                if (_meta != null && _meta.IsValidSaveSlot(i))
                {
                    MetaProgressionService.SaveSlotSummary summary;
                    if (_meta.TryGetSlotSummary(i, out summary))
                    {
                        hasData = summary.HasData;
                        totalCash = summary.TotalCash;
                        regionId = summary.SelectedRegionId;
                        isActive = i == _meta.ActiveSaveSlotIndex;
                    }
                }

                string actionLabel = hasData ? "CONTINUE" : "NEW GAME";
                label.text =
                    "SLOT " + (i + 1) + (isActive ? " [ACTIVE]" : string.Empty) + "\n" +
                    (hasData
                        ? "Saved: $" + totalCash + " / " + LobbyRegionMetaUtil.FormatRegionName(regionId)
                        : "Empty Slot") +
                    "\nClick: " + actionLabel;

                Image buttonImage = slotButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = hasData
                        ? new Color(0.82f, 0.9f, 1f, 0.98f)
                        : new Color(0.94f, 0.95f, 0.99f, 0.98f);
                }

                slotButton.interactable = true;
            }
        }

        private void SetSaveSlotPanelVisible(bool visible)
        {
            if (_slotSelectPanel != null)
            {
                _slotSelectPanel.SetActive(visible);
            }
        }

        private void SetSettingsPanelVisible(bool visible)
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(visible);
            }
        }

        private void PlayUiClick()
        {
            if (_audio == null || _catalog == null)
            {
                return;
            }

            _audio.PlayUiClick(_catalog.UiClickKey);
        }

        private static Image CreatePanelImage(RectTransform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = Color.white;
            return image;
        }

        private static Text CreateText(
            RectTransform parent,
            Font font,
            string text,
            int size,
            FontStyle style,
            TextAnchor anchor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.color = color;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(
            RectTransform parent,
            Font font,
            string text,
            Vector2 anchoredPosition,
            Vector2 size,
            bool accent)
        {
            GameObject go = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = accent
                ? new Color(0.2f, 0.45f, 0.9f, 0.98f)
                : new Color(0.9f, 0.92f, 0.97f, 0.98f);

            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.84f, 0.86f, 0.94f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.55f);
            button.colors = colors;

            Text label = CreateText(
                rect,
                font,
                text,
                23,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                accent ? Color.white : new Color(0.06f, 0.07f, 0.1f, 1f));
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            return button;
        }
    }
}
