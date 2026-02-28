using DeliveryRun;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Lobby;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class LobbyHubManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private const float ScanInterval = 0.1f;

        private static readonly string[] UpgradeIds =
        {
            "bike_speed", "bike_turn", "bike_accel", "music_luck", "bike_grip", "bike_brake", "reward_bonus"
        };

        private static readonly string[] RegionIds =
        {
            "central", "rushdistrict", "frostlands", "hillcrest", "stormcoast", "oldtown", "seaside"
        };

        private static readonly string[] UpgradeLabels =
        {
            "Speed", "Turn", "Accel", "Luck", "Grip", "Brake", "Reward"
        };

        private LobbyAvatarController _player;
        private readonly LobbyInteractZone[] _zones = new LobbyInteractZone[24];
        private int _zoneCount;
        private LobbyInteractZone _activeZone;

        private MetaProgressionService _meta;
        private UiPrefabCatalogSO _catalog;
        private AudioManager _audio;

        private bool _isLobby;
        private float _scenePollElapsed;
        private float _scanElapsed;

        private GameObject _uiRoot;
        private Text _promptText;
        private Text _metaText;
        private GameObject _garagePanel;
        private readonly Text[] _upgradeRows = new Text[7];
        private readonly Button[] _upgradeButtons = new Button[7];
        private GameObject _regionPanel;
        private Text _regionText;
        private Button _unlockButton;
        private Text _unlockButtonText;
        private string _pendingUnlockRegionId;
        private Sprite _panelSkinSprite;
        private Sprite _buttonSkinSprite;
        private Sprite _buttonAccentSkinSprite;

        public override string Name => nameof(LobbyHubManager);
        public override int InitOrder => 33;

        protected override void OnInitialize()
        {
            Services.TryGet(out _meta);
            Services.TryGet(out _audio);
            _catalog = UiPrefabCatalogLoader.LoadOrNull();

            Subs.Add<SceneTransitionStarted>(Events, OnTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnTransitionCompleted);
            Subs.Add<MetaBalanceChanged>(Events, OnMetaDirty);
            Subs.Add<PermanentUpgradeChanged>(Events, OnMetaDirty);
            Subs.Add<SelectedRegionChanged>(Events, OnMetaDirty);
            Subs.Add<RegionUnlockStatusChanged>(Events, OnMetaDirty);
            Subs.Add<RegionUnlocked>(Events, OnMetaDirty);
            Subs.Add<RegionUnlockFailed>(Events, OnMetaDirty);

            HandleScene(SceneManager.GetActiveScene().name);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollElapsed += unscaledDeltaTime;
            if (_scenePollElapsed >= ScenePollInterval)
            {
                _scenePollElapsed = 0f;
                HandleScene(SceneManager.GetActiveScene().name);
            }

            if (!_isLobby)
            {
                return;
            }

            EnsureRefs();
            _scanElapsed += unscaledDeltaTime;
            if (_scanElapsed >= ScanInterval)
            {
                _scanElapsed = 0f;
                ScanNearestZone();
                RefreshPrompt();
            }

            if (_activeZone != null && RuntimeInput.ConsumeInteractPressedThisFrame())
            {
                Interact(_activeZone);
            }
        }

        protected override void OnShutdown()
        {
            _isLobby = false;
            DestroyUi();
        }

        private void OnTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.LobbyScene)
            {
                return;
            }

            _isLobby = false;
            DestroyUi();
        }

        private void OnTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleScene(evt.SceneName);
        }

        private void OnMetaDirty<T>(T evt)
        {
            RefreshMeta();
        }

        private void HandleScene(string sceneName)
        {
            bool shouldLobby = sceneName == SceneNames.LobbyScene;
            if (_isLobby == shouldLobby)
            {
                if (_isLobby)
                {
                    EnsureRefs();
                    EnsureUi();
                }
                return;
            }

            _isLobby = shouldLobby;
            _zoneCount = 0;
            _activeZone = null;
            if (_isLobby)
            {
                EnsureRefs();
                EnsureUi();
                return;
            }

            DestroyUi();
        }

        private void EnsureRefs()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<LobbyAvatarController>();
            }

            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            if (_audio == null)
            {
                Services.TryGet(out _audio);
            }

            if (_zoneCount <= 0)
            {
                LobbyInteractZone[] zones = Object.FindObjectsByType<LobbyInteractZone>(FindObjectsSortMode.None);
                _zoneCount = zones.Length < _zones.Length ? zones.Length : _zones.Length;
                for (int i = 0; i < _zoneCount; i++)
                {
                    _zones[i] = zones[i];
                }
            }
        }

        private void ScanNearestZone()
        {
            _activeZone = null;
            if (_player == null)
            {
                return;
            }

            float best = float.MaxValue;
            Vector3 p = _player.transform.position;
            for (int i = 0; i < _zoneCount; i++)
            {
                LobbyInteractZone zone = _zones[i];
                if (zone == null || !zone.gameObject.activeInHierarchy || !zone.IsInRange(p))
                {
                    continue;
                }

                Vector3 d = zone.transform.position - p;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr >= best)
                {
                    continue;
                }

                best = sqr;
                _activeZone = zone;
            }
        }

        private void Interact(LobbyInteractZone zone)
        {
            if (zone == null)
            {
                return;
            }

            PlayUiClick();
            switch (zone.InteractType)
            {
                case LobbyInteractType.StartDelivery:
                    Events.Publish(new StartRunRequested());
                    break;
                case LobbyInteractType.OpenGaragePanel:
                    TogglePanel(_garagePanel);
                    break;
                case LobbyInteractType.OpenRegionPanel:
                    TogglePanel(_regionPanel);
                    break;
                case LobbyInteractType.OpenSettingsPanel:
                    break;
                case LobbyInteractType.ExitGame:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        private void EnsureUi()
        {
            if (_uiRoot != null)
            {
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

            _metaText = CreateText(root, font, 22, TextAnchor.UpperLeft, new Vector2(24f, -24f), new Vector2(860f, 92f));
            _promptText = CreateText(root, font, 30, TextAnchor.MiddleCenter, new Vector2(0f, 72f), new Vector2(980f, 52f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(1f, 0.9f, 0.22f, 1f));

            _garagePanel = BuildGaragePanel(root, font);
            _regionPanel = BuildRegionPanel(root, font);

            RefreshMeta();
            RefreshPrompt();
        }

        private GameObject BuildGaragePanel(RectTransform root, Font font)
        {
            GameObject panel = CreatePanel(root, "GaragePanel", new Vector2(160f, -84f), new Vector2(760f, 700f));
            CreateText(panel.GetComponent<RectTransform>(), font, 34, TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(0f, 44f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.9f, 0.22f, 1f), "GARAGE UPGRADES");
            CreateText(panel.GetComponent<RectTransform>(), font, 18, TextAnchor.UpperCenter, new Vector2(0f, -70f), new Vector2(700f, 32f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Color(0.82f, 0.9f, 0.98f, 0.92f), "Upgrade bike performance and run rewards.");

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                float y = -130f - (i * 72f);
                _upgradeRows[i] = CreateText(panel.GetComponent<RectTransform>(), font, 22, TextAnchor.MiddleLeft, new Vector2(24f, y), new Vector2(520f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.92f, 0.96f, 1f, 1f));
                Button button = CreateButton(panel.GetComponent<RectTransform>(), font, "UPGRADE", new Vector2(-24f, y), new Vector2(188f, 44f), true);
                RectTransform br = button.GetComponent<RectTransform>();
                br.anchorMin = new Vector2(1f, 1f);
                br.anchorMax = new Vector2(1f, 1f);
                br.pivot = new Vector2(1f, 1f);
                int captured = i;
                button.onClick.AddListener(() => OnUpgradeClicked(captured));
                _upgradeButtons[i] = button;
            }

            Button close = CreateButton(panel.GetComponent<RectTransform>(), font, "CLOSE", new Vector2(0f, 22f), new Vector2(180f, 44f));
            close.onClick.AddListener(() => TogglePanel(_garagePanel, false));
            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildRegionPanel(RectTransform root, Font font)
        {
            GameObject panel = CreatePanel(root, "RegionPanel", new Vector2(960f, -84f), new Vector2(780f, 700f));
            CreateText(panel.GetComponent<RectTransform>(), font, 34, TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(0f, 44f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Color(1f, 0.9f, 0.22f, 1f), "REGION EXPANSION");
            _regionText = CreateText(panel.GetComponent<RectTransform>(), font, 21, TextAnchor.UpperLeft, new Vector2(24f, -86f), new Vector2(730f, 460f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.92f, 0.96f, 1f, 1f));

            Button next = CreateButton(panel.GetComponent<RectTransform>(), font, "NEXT REGION", new Vector2(-130f, 84f), new Vector2(220f, 48f));
            next.onClick.AddListener(() =>
            {
                PlayUiClick();
                Events.Publish(new SelectNextRegionRequested());
            });

            _unlockButton = CreateButton(panel.GetComponent<RectTransform>(), font, "UNLOCK", new Vector2(130f, 84f), new Vector2(220f, 48f), true);
            _unlockButton.onClick.AddListener(OnUnlockClicked);
            _unlockButtonText = _unlockButton.GetComponentInChildren<Text>();

            Button close = CreateButton(panel.GetComponent<RectTransform>(), font, "CLOSE", new Vector2(0f, 24f), new Vector2(180f, 44f));
            close.onClick.AddListener(() => TogglePanel(_regionPanel, false));
            panel.SetActive(false);
            return panel;
        }

        private void OnUpgradeClicked(int index)
        {
            if (index < 0 || index >= UpgradeIds.Length)
            {
                return;
            }

            PlayUiClick();
            Events.Publish(new PermanentUpgradePurchaseRequested { UpgradeId = UpgradeIds[index] });
        }

        private void OnUnlockClicked()
        {
            if (string.IsNullOrEmpty(_pendingUnlockRegionId))
            {
                return;
            }

            PlayUiClick();
            Events.Publish(new UnlockRegionRequested { RegionId = _pendingUnlockRegionId });
        }

        private void RefreshMeta()
        {
            if (_metaText == null)
            {
                return;
            }

            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            if (_meta == null)
            {
                _metaText.text = "Meta unavailable";
                return;
            }

            _metaText.text = "Total Cash: $" + _meta.TotalCash + "\nSelected Region: " + FormatRegion(_meta.SelectedRegionId);
            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                if (_upgradeRows[i] == null || _upgradeButtons[i] == null)
                {
                    continue;
                }

                int level = _meta.GetUpgradeLevel(UpgradeIds[i]);
                if (level >= 3)
                {
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " (MAX)";
                    _upgradeButtons[i].interactable = false;
                }
                else
                {
                    int price = UpgradePrice(level + 1);
                    _upgradeRows[i].text = UpgradeLabels[i] + " Lv." + level + " -> Lv." + (level + 1) + "  $" + price;
                    _upgradeButtons[i].interactable = _meta.TotalCash >= price;
                }
            }

            if (_regionText != null)
            {
                var regionBuilder = new StringBuilder(512);
                regionBuilder.Append("Unlocked Regions\n");
                for (int i = 0; i < RegionIds.Length; i++)
                {
                    string regionId = RegionIds[i];
                    bool unlocked = _meta.IsRegionUnlocked(regionId);
                    regionBuilder.Append(unlocked ? "[OPEN] " : "[LOCK] ");
                    if (string.Equals(regionId, _meta.SelectedRegionId, System.StringComparison.Ordinal))
                    {
                        regionBuilder.Append("* ");
                    }

                    regionBuilder.Append(FormatRegion(regionId));
                    regionBuilder.Append('\n');
                }

                RegionUnlockRule rule;
                if (!RegionProgressionCatalog.TryGetNextLockedRegion(_meta, out _pendingUnlockRegionId) ||
                    !RegionProgressionCatalog.TryGetRule(_pendingUnlockRegionId, out rule))
                {
                    regionBuilder.Append("\nAll regions unlocked.");
                    _regionText.text = regionBuilder.ToString();
                    if (_unlockButton != null) _unlockButton.interactable = false;
                    if (_unlockButtonText != null) _unlockButtonText.text = "ALL";
                }
                else
                {
                    int cash = _meta.GetBestRunCash(rule.PreviousRegionId);
                    float rating = _meta.GetBestRunRating(rule.PreviousRegionId);
                    bool prev = _meta.IsRegionUnlocked(rule.PreviousRegionId);
                    bool canUnlock = prev && cash >= rule.RequiredRunCash && rating >= rule.RequiredRunRating && _meta.TotalCash >= rule.UnlockCost;
                    regionBuilder.Append("\nNext Unlock: ");
                    regionBuilder.Append(FormatRegion(rule.RegionId));
                    regionBuilder.Append("\nNeed previous: ");
                    regionBuilder.Append(FormatRegion(rule.PreviousRegionId));
                    regionBuilder.Append("\nBest Cash: $");
                    regionBuilder.Append(cash);
                    regionBuilder.Append(" / $");
                    regionBuilder.Append(rule.RequiredRunCash);
                    regionBuilder.Append("\nBest Rating: ");
                    regionBuilder.Append(rating.ToString("0.0"));
                    regionBuilder.Append(" / ");
                    regionBuilder.Append(rule.RequiredRunRating.ToString("0.0"));
                    regionBuilder.Append("\nUnlock Cost: $");
                    regionBuilder.Append(rule.UnlockCost);

                    _regionText.text = regionBuilder.ToString();
                    if (_unlockButton != null) _unlockButton.interactable = canUnlock;
                    if (_unlockButtonText != null) _unlockButtonText.text = canUnlock ? "UNLOCK" : "LOCKED";
                }
            }
        }

        private void RefreshPrompt()
        {
            if (_promptText == null)
            {
                return;
            }

            _promptText.text = _activeZone == null ? string.Empty : (_activeZone.PromptText + " [F]");
        }

        private void TogglePanel(GameObject panel)
        {
            TogglePanel(panel, panel != null && !panel.activeSelf);
        }

        private void TogglePanel(GameObject panel, bool active)
        {
            if (_garagePanel != null && _garagePanel != panel) _garagePanel.SetActive(false);
            if (_regionPanel != null && _regionPanel != panel) _regionPanel.SetActive(false);
            if (panel != null) panel.SetActive(active);
            RefreshMeta();
        }

        private void PlayUiClick()
        {
            if (_audio == null || _catalog == null)
            {
                return;
            }

            _audio.PlayUiClick(_catalog.UiClickKey);
        }

        private void EnsureUiSkins()
        {
            if (_panelSkinSprite != null || _buttonSkinSprite != null || _buttonAccentSkinSprite != null)
            {
                return;
            }

            Texture2D panelTexture = _catalog != null ? _catalog.LobbyPanelTexture : null;
            Texture2D buttonTexture = _catalog != null ? _catalog.LobbyButtonTexture : null;
            Texture2D accentTexture = _catalog != null ? _catalog.LobbyButtonAccentTexture : null;

            _panelSkinSprite = CreateSpriteFromTexture(panelTexture, new Vector4(28f, 28f, 28f, 28f));
            _buttonSkinSprite = CreateSpriteFromTexture(buttonTexture, new Vector4(22f, 22f, 22f, 22f));
            _buttonAccentSkinSprite = CreateSpriteFromTexture(accentTexture, new Vector4(22f, 22f, 22f, 22f));
        }

        private static Sprite CreateSpriteFromTexture(Texture2D texture, Vector4 border)
        {
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                border);
        }

        private void ReleaseUiSkins()
        {
            if (_panelSkinSprite != null)
            {
                Object.Destroy(_panelSkinSprite);
                _panelSkinSprite = null;
            }

            if (_buttonSkinSprite != null)
            {
                Object.Destroy(_buttonSkinSprite);
                _buttonSkinSprite = null;
            }

            if (_buttonAccentSkinSprite != null)
            {
                Object.Destroy(_buttonAccentSkinSprite);
                _buttonAccentSkinSprite = null;
            }
        }

        private void DestroyUi()
        {
            _promptText = null;
            _metaText = null;
            _garagePanel = null;
            _regionPanel = null;
            _regionText = null;
            _unlockButton = null;
            _unlockButtonText = null;
            _pendingUnlockRegionId = null;
            for (int i = 0; i < _upgradeRows.Length; i++)
            {
                _upgradeRows[i] = null;
                _upgradeButtons[i] = null;
            }

            if (_uiRoot != null)
            {
                Object.Destroy(_uiRoot);
                _uiRoot = null;
            }

            ReleaseUiSkins();
        }

        private static int UpgradePrice(int targetLevel)
        {
            if (targetLevel <= 1) return 1200;
            if (targetLevel == 2) return 2800;
            return 5600;
        }

        private static string FormatRegion(string id)
        {
            if (string.IsNullOrEmpty(id)) return "CENTRAL";
            if (id == "rushdistrict") return "RUSH DISTRICT";
            if (id == "frostlands") return "FROSTLANDS";
            if (id == "hillcrest") return "HILLCREST";
            if (id == "stormcoast") return "STORM COAST";
            if (id == "oldtown") return "OLD TOWN";
            if (id == "seaside") return "SEASIDE";
            if (id == "central") return "CENTRAL";
            return id.ToUpperInvariant();
        }

        private GameObject CreatePanel(RectTransform parent, string name, Vector2 anchored, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            if (_panelSkinSprite != null)
            {
                image.sprite = _panelSkinSprite;
                image.type = Image.Type.Sliced;
                image.color = new Color(1f, 1f, 1f, 0.98f);
            }
            else
            {
                image.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);
            }
            return go;
        }

        private static Text CreateText(
            RectTransform parent,
            Font font,
            int size,
            TextAnchor anchor,
            Vector2 anchoredPos,
            Vector2 boxSize,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null,
            Color? color = null,
            string text = "")
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin ?? new Vector2(0f, 1f);
            rt.anchorMax = anchorMax ?? new Vector2(0f, 1f);
            rt.pivot = pivot ?? new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = boxSize;
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }

        private Button CreateButton(RectTransform parent, Font font, string label, Vector2 anchoredPos, Vector2 size, bool accent = false)
        {
            GameObject go = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image bg = go.GetComponent<Image>();
            Sprite skin = accent && _buttonAccentSkinSprite != null ? _buttonAccentSkinSprite : _buttonSkinSprite;
            if (skin != null)
            {
                bg.sprite = skin;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = accent ? new Color(0.74f, 0.55f, 0.14f, 0.98f) : new Color(0.18f, 0.24f, 0.34f, 0.98f);
            }

            Button b = go.GetComponent<Button>();
            ColorBlock colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 0.96f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.74f, 0.74f, 0.74f, 0.62f);
            b.colors = colors;

            Color textColor = accent
                ? new Color(0.13f, 0.18f, 0.25f, 1f)
                : new Color(0.97f, 0.97f, 0.99f, 1f);
            Text t = CreateText(rt, font, 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), textColor, label);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            return b;
        }
    }
}
