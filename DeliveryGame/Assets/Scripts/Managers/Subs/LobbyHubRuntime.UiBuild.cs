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
            _promptText = CreateText(
                root,
                font,
                30,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 72f),
                new Vector2(980f, 52f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Color(1f, 0.9f, 0.22f, 1f));

            _garagePanel = BuildGaragePanel(root, font);
            _regionPanel = BuildRegionPanel(root, font);

            RefreshMeta();
            RefreshPrompt();
        }

        private GameObject BuildGaragePanel(RectTransform root, Font font)
        {
            GameObject panel = CreatePanel(root, "GaragePanel", new Vector2(160f, -84f), new Vector2(760f, 700f));
            CreateText(
                panel.GetComponent<RectTransform>(),
                font,
                34,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -24f),
                new Vector2(0f, 44f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Color(1f, 0.9f, 0.22f, 1f),
                "GARAGE UPGRADES");
            CreateText(
                panel.GetComponent<RectTransform>(),
                font,
                18,
                TextAnchor.UpperCenter,
                new Vector2(0f, -70f),
                new Vector2(700f, 32f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Color(0.82f, 0.9f, 0.98f, 0.92f),
                "Upgrade bike performance and run rewards.");

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                float y = -130f - (i * 72f);
                _upgradeRows[i] = CreateText(
                    panel.GetComponent<RectTransform>(),
                    font,
                    22,
                    TextAnchor.MiddleLeft,
                    new Vector2(24f, y),
                    new Vector2(520f, 44f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Color(0.92f, 0.96f, 1f, 1f));

                Button button = CreateButton(
                    panel.GetComponent<RectTransform>(),
                    font,
                    "UPGRADE",
                    new Vector2(-24f, y),
                    new Vector2(188f, 44f),
                    true);
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
            CreateText(
                panel.GetComponent<RectTransform>(),
                font,
                34,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -24f),
                new Vector2(0f, 44f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Color(1f, 0.9f, 0.22f, 1f),
                "REGION EXPANSION");
            _regionText = CreateText(
                panel.GetComponent<RectTransform>(),
                font,
                21,
                TextAnchor.UpperLeft,
                new Vector2(24f, -86f),
                new Vector2(730f, 460f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Color(0.92f, 0.96f, 1f, 1f));

            Button next = CreateButton(panel.GetComponent<RectTransform>(), font, "NEXT REGION", new Vector2(-130f, 84f), new Vector2(220f, 48f));
            next.onClick.AddListener(() =>
            {
                PlayUiClick();
                _events.Publish(new SelectNextRegionRequested());
            });

            _unlockButton = CreateButton(panel.GetComponent<RectTransform>(), font, "UNLOCK", new Vector2(130f, 84f), new Vector2(220f, 48f), true);
            _unlockButton.onClick.AddListener(OnUnlockClicked);
            _unlockButtonText = _unlockButton.GetComponentInChildren<Text>();

            Button close = CreateButton(panel.GetComponent<RectTransform>(), font, "CLOSE", new Vector2(0f, 24f), new Vector2(180f, 44f));
            close.onClick.AddListener(() => TogglePanel(_regionPanel, false));
            panel.SetActive(false);
            return panel;
        }
    }
}
