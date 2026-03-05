#if UNITY_EDITOR
using System;
using System.IO;
using DeliveryRun.UI.Lobby;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeliveryRun.EditorTools
{
    public static class LobbySceneBuilder
    {
        public const string LobbyScenePath = "Assets/Scenes/Lobby/LobbyScene.unity";
        public const string LobbyPrefabPath = "Assets/Prefabs/UI/Lobby/LobbyUIRoot.prefab";
        public const string LobbyAddressKey = "ui/lobby/lobby_ui_root";

        public static void BuildOrUpdateLobbyScene()
        {
            BuildOrUpdateLobbySceneInternal(placePrefabInScene: false);
        }

        public static void BuildOrUpdateLobbySceneWithEmbeddedUi()
        {
            BuildOrUpdateLobbySceneInternal(placePrefabInScene: true);
        }

        public static void BuildOrUpdateLobbyPrefab()
        {
            EnsureFolders();
            BuildLobbyUiPrefab();
            LobbyUIAddressablesConfigurator.TryConfigureLobbyUiAddressable(LobbyPrefabPath, LobbyAddressKey);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LobbySceneBuilder] Lobby UI prefab updated.");
        }

        private static void BuildOrUpdateLobbySceneInternal(bool placePrefabInScene)
        {
            EnsureFolders();
            GameObject prefab = BuildLobbyUiPrefab();
            LobbyUIAddressablesConfigurator.TryConfigureLobbyUiAddressable(LobbyPrefabPath, LobbyAddressKey);

            Scene scene = LoadOrCreateLobbyScene();
            ClearSceneRoots(scene);

            EnsureEventSystemInScene();

            GameObject bootstrapObject = new GameObject("LobbyUIBootstrap");
            LobbyUIBootstrap bootstrap = bootstrapObject.AddComponent<LobbyUIBootstrap>();
            bootstrap.ConfigureForEditor(prefab, LobbyAddressKey);

            if (placePrefabInScene && prefab != null)
            {
                PrefabUtility.InstantiatePrefab(prefab, scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LobbySceneBuilder] Lobby scene updated. bootstrapOnly=" + (!placePrefabInScene));
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder("Assets/Prefabs/UI/Lobby");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Scenes/Lobby");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string normalized = folderPath.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            if (slash <= 0)
            {
                return;
            }

            string parent = normalized.Substring(0, slash);
            string leaf = normalized.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static Scene LoadOrCreateLobbyScene()
        {
            if (File.Exists(LobbyScenePath))
            {
                return EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            return scene;
        }

        private static void ClearSceneRoots(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }
        }

        private static void EnsureEventSystemInScene()
        {
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                eventSystemObject.AddComponent(inputSystemModuleType);
            }
            else
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static GameObject BuildLobbyUiPrefab()
        {
            TMP_FontAsset font = ResolveTmpFont();
            Sprite externalSprite = FindExternalSprite();

            GameObject root = new GameObject(
                "LobbyUIRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(LobbyUIRootView),
                typeof(LobbyUIController));

            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            GameObject safeArea = CreateUiObject("SafeArea", root.transform, typeof(RectTransform), typeof(SafeAreaFitter));
            RectTransform safeAreaRect = safeArea.GetComponent<RectTransform>();
            Stretch(safeAreaRect);

            GameObject background = CreateUiObject("Background", safeArea.transform, typeof(RectTransform), typeof(Image));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            Stretch(backgroundRect);
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.06f, 0.09f, 0.14f, 1f);
            if (externalSprite != null)
            {
                backgroundImage.sprite = externalSprite;
                backgroundImage.type = Image.Type.Sliced;
                backgroundImage.color = new Color(1f, 1f, 1f, 0.16f);
                backgroundImage.preserveAspect = true;
            }

            GameObject top = CreateUiObject("Top", safeArea.transform, typeof(RectTransform));
            RectTransform topRect = top.GetComponent<RectTransform>();
            SetAnchors(topRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 180f);

            TextMeshProUGUI title = CreateText(
                "Title",
                topRect,
                font,
                "DELIVERY RUN",
                88,
                TextAlignmentOptions.Center,
                new Color(1f, 0.92f, 0.28f, 1f));
            RectTransform titleRect = title.rectTransform;
            Stretch(titleRect);
            titleRect.offsetMin = new Vector2(24f, 24f);
            titleRect.offsetMax = new Vector2(-24f, -24f);

            TextMeshProUGUI version = CreateText(
                "VersionText",
                topRect,
                font,
                "v0.1 UI Draft",
                22,
                TextAlignmentOptions.TopRight,
                new Color(0.85f, 0.92f, 1f, 0.85f));
            RectTransform versionRect = version.rectTransform;
            SetAnchors(versionRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            versionRect.anchoredPosition = new Vector2(-24f, -22f);
            versionRect.sizeDelta = new Vector2(260f, 42f);

            GameObject body = CreateUiObject("Body", safeArea.transform, typeof(RectTransform));
            RectTransform bodyRect = body.GetComponent<RectTransform>();
            Stretch(bodyRect);
            bodyRect.offsetMin = new Vector2(220f, 80f);
            bodyRect.offsetMax = new Vector2(-220f, -190f);

            GameObject mainMenuPanel = CreateUiObject("MainMenuPanel", body.transform, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform mainMenuRect = mainMenuPanel.GetComponent<RectTransform>();
            Stretch(mainMenuRect);
            Image mainMenuImage = mainMenuPanel.GetComponent<Image>();
            mainMenuImage.color = new Color(0.08f, 0.12f, 0.18f, 0.94f);

            CreateText(
                "Header",
                mainMenuRect,
                font,
                "MAIN MENU",
                42,
                TextAlignmentOptions.Center,
                new Color(0.93f, 0.96f, 1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -56f),
                new Vector2(0f, 66f));

            GameObject mainButtonColumn = CreateUiObject(
                "MainButtonColumn",
                mainMenuRect,
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            RectTransform mainColumnRect = mainButtonColumn.GetComponent<RectTransform>();
            SetAnchors(mainColumnRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            mainColumnRect.anchoredPosition = new Vector2(0f, -14f);
            mainColumnRect.sizeDelta = new Vector2(460f, 0f);

            VerticalLayoutGroup mainLayout = mainButtonColumn.GetComponent<VerticalLayoutGroup>();
            mainLayout.spacing = 14f;
            mainLayout.padding = new RectOffset(0, 0, 0, 0);
            mainLayout.childAlignment = TextAnchor.MiddleCenter;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;

            ContentSizeFitter mainFitter = mainButtonColumn.GetComponent<ContentSizeFitter>();
            mainFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            mainFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button deliveryStartButton = CreateButton(mainButtonColumn.transform, font, "Delivery Start", true);
            Button regionUnlockButton = CreateButton(mainButtonColumn.transform, font, "Region Unlock", false);
            Button upgradeButton = CreateButton(mainButtonColumn.transform, font, "Upgrade", false);
            Button exitButton = CreateButton(mainButtonColumn.transform, font, "Exit", false);

            GameObject regionUnlockPanel = CreateUiObject("RegionUnlockPanel", body.transform, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform regionRect = regionUnlockPanel.GetComponent<RectTransform>();
            Stretch(regionRect);
            Image regionImage = regionUnlockPanel.GetComponent<Image>();
            regionImage.color = new Color(0.08f, 0.12f, 0.18f, 0.94f);

            CreateText(
                "Header",
                regionRect,
                font,
                "REGION UNLOCK",
                38,
                TextAlignmentOptions.Center,
                new Color(0.95f, 0.97f, 1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -44f),
                new Vector2(0f, 56f));

            Button regionBackButton = CreateBackButton(regionRect, font);

            GameObject previewRoot = CreateUiObject("WorldMapPreviewPlaceholder", regionRect, typeof(RectTransform), typeof(Image));
            RectTransform previewRect = previewRoot.GetComponent<RectTransform>();
            SetAnchors(previewRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            previewRect.anchoredPosition = new Vector2(0f, -10f);
            previewRect.sizeDelta = new Vector2(900f, 430f);
            Image previewImage = previewRoot.GetComponent<Image>();
            previewImage.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);
            if (externalSprite != null)
            {
                previewImage.sprite = externalSprite;
                previewImage.type = Image.Type.Sliced;
                previewImage.color = new Color(1f, 1f, 1f, 0.18f);
            }

            GameObject previewRawGo = CreateUiObject("MapPreview", previewRect, typeof(RectTransform), typeof(RawImage));
            RectTransform previewRawRect = previewRawGo.GetComponent<RectTransform>();
            Stretch(previewRawRect);
            previewRawRect.offsetMin = new Vector2(12f, 12f);
            previewRawRect.offsetMax = new Vector2(-12f, -12f);
            RawImage previewRaw = previewRawGo.GetComponent<RawImage>();
            previewRaw.color = new Color(0.18f, 0.3f, 0.44f, 0.7f);
            if (externalSprite != null)
            {
                previewRaw.texture = externalSprite.texture;
            }

            CreateText(
                "PlaceholderText",
                previewRect,
                font,
                "Map Preview Placeholder",
                28,
                TextAlignmentOptions.Center,
                new Color(0.9f, 0.96f, 1f, 0.96f));

            GameObject upgradePanel = CreateUiObject("UpgradePanel", body.transform, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform upgradeRect = upgradePanel.GetComponent<RectTransform>();
            Stretch(upgradeRect);
            Image upgradeImage = upgradePanel.GetComponent<Image>();
            upgradeImage.color = new Color(0.08f, 0.12f, 0.18f, 0.94f);

            CreateText(
                "Header",
                upgradeRect,
                font,
                "UPGRADE",
                38,
                TextAlignmentOptions.Center,
                new Color(0.95f, 0.97f, 1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -44f),
                new Vector2(0f, 56f));

            Button upgradeBackButton = CreateBackButton(upgradeRect, font);

            GameObject scrollRoot = CreateUiObject("UpgradeScrollPlaceholder", upgradeRect, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            RectTransform scrollRect = scrollRoot.GetComponent<RectTransform>();
            SetAnchors(scrollRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            scrollRect.anchoredPosition = new Vector2(0f, -10f);
            scrollRect.sizeDelta = new Vector2(900f, 430f);
            Image scrollImage = scrollRoot.GetComponent<Image>();
            scrollImage.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);

            GameObject viewport = CreateUiObject("Viewport", scrollRoot.transform, typeof(RectTransform), typeof(Image), typeof(Mask));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            viewportRect.offsetMin = new Vector2(12f, 12f);
            viewportRect.offsetMax = new Vector2(-12f, -12f);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.06f);
            Mask mask = viewport.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = CreateUiObject("Content", viewport.transform, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform contentRect = content.GetComponent<RectTransform>();
            SetAnchors(contentRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 10f;
            contentLayout.padding = new RectOffset(14, 14, 14, 14);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateScrollRow(content.transform, font, "Upgrade slot placeholder #1");
            CreateScrollRow(content.transform, font, "Upgrade slot placeholder #2");
            CreateScrollRow(content.transform, font, "Upgrade slot placeholder #3");

            ScrollRect scrollComponent = scrollRoot.GetComponent<ScrollRect>();
            scrollComponent.viewport = viewportRect;
            scrollComponent.content = contentRect;
            scrollComponent.horizontal = false;
            scrollComponent.vertical = true;
            scrollComponent.movementType = ScrollRect.MovementType.Clamped;
            scrollComponent.scrollSensitivity = 24f;

            regionUnlockPanel.SetActive(false);
            upgradePanel.SetActive(false);

            LobbyUIRootView view = root.GetComponent<LobbyUIRootView>();
            view.BindForBuild(
                mainMenuPanel,
                regionUnlockPanel,
                upgradePanel,
                deliveryStartButton,
                regionUnlockButton,
                upgradeButton,
                exitButton,
                regionBackButton,
                upgradeBackButton);
            view.ShowPanel(LobbyPanel.MainMenu);

            LobbyUIController controller = root.GetComponent<LobbyUIController>();
            controller.SetView(view);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static TMP_FontAsset ResolveTmpFont()
        {
            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
            {
                return defaultFont;
            }

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                TMP_FontAsset loaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (loaded != null)
                {
                    return loaded;
                }
            }

            return null;
        }

        private static Sprite FindExternalSprite()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Externals"))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Externals" });
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Button CreateButton(Transform parent, TMP_FontAsset font, string label, bool accent)
        {
            GameObject buttonObject = CreateUiObject("Button_" + label.Replace(" ", string.Empty), parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0f, 70f);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 70f;
            layout.minHeight = 62f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = accent ? new Color(0.17f, 0.53f, 0.88f, 1f) : new Color(0.16f, 0.24f, 0.34f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(image.color, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.85f);
            button.colors = colors;

            TextMeshProUGUI text = CreateText(
                "Label",
                buttonRect,
                font,
                label,
                30,
                TextAlignmentOptions.Center,
                new Color(0.95f, 0.97f, 1f, 1f));
            RectTransform textRect = text.rectTransform;
            Stretch(textRect);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static Button CreateBackButton(RectTransform parent, TMP_FontAsset font)
        {
            GameObject backObject = CreateUiObject("BackButton", parent, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform backRect = backObject.GetComponent<RectTransform>();
            SetAnchors(backRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            backRect.anchoredPosition = new Vector2(20f, -18f);
            backRect.sizeDelta = new Vector2(120f, 44f);

            Image backImage = backObject.GetComponent<Image>();
            backImage.color = new Color(0.21f, 0.28f, 0.38f, 1f);

            Button backButton = backObject.GetComponent<Button>();
            ColorBlock colors = backButton.colors;
            colors.normalColor = backImage.color;
            colors.highlightedColor = Color.Lerp(backImage.color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(backImage.color, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.85f);
            backButton.colors = colors;

            TextMeshProUGUI text = CreateText(
                "Label",
                backRect,
                font,
                "BACK",
                24,
                TextAlignmentOptions.Center,
                new Color(0.93f, 0.96f, 1f, 1f));
            RectTransform textRect = text.rectTransform;
            Stretch(textRect);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return backButton;
        }

        private static void CreateScrollRow(Transform parent, TMP_FontAsset font, string text)
        {
            GameObject row = CreateUiObject("Row", parent, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 72f);

            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = 72f;
            layout.minHeight = 64f;

            Image rowImage = row.GetComponent<Image>();
            rowImage.color = new Color(0.2f, 0.28f, 0.4f, 0.85f);

            TextMeshProUGUI label = CreateText(
                "RowText",
                rowRect,
                font,
                text,
                26,
                TextAlignmentOptions.Center,
                new Color(0.95f, 0.98f, 1f, 1f));
            RectTransform textRect = label.rectTransform;
            Stretch(textRect);
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);
        }

        private static TextMeshProUGUI CreateText(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string text,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null,
            Vector2? anchoredPosition = null,
            Vector2? sizeDelta = null)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin ?? Vector2.zero;
            rect.anchorMax = anchorMax ?? Vector2.one;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
            rect.sizeDelta = sizeDelta ?? Vector2.zero;

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = true;
            return label;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            GameObject go = new GameObject(name, components);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }
    }
}
#endif
