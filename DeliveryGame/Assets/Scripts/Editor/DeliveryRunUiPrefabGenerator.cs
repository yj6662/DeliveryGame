using System;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunUiPrefabGenerator
    {
        private const string PrefabFolder = "Assets/Prefabs/UI/Run";
        private const string RunHudPrefabPath = PrefabFolder + "/RunHUD.prefab";
        private const string MusicModalPrefabPath = PrefabFolder + "/MusicSelectionModal.prefab";
        private const string RunResultModalPrefabPath = PrefabFolder + "/RunResultModal.prefab";
        private const string CatalogPath = "Assets/Resources/Bootstrap/UiPrefabCatalog.asset";
        private const string ExternalsFolder = "Assets/Externals";
        private const string KenneyUiPngFolder = "Assets/Externals/kenney_ui-pack/PNG";
        private const string KenneyUiFontFolder = "Assets/Externals/kenney_ui-pack/Font";

        [MenuItem("Tools/DeliveryRun/Generate UI Prefabs")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Prefabs/UI/Run");
            EnsureFolder("Assets/Scripts/UI/Run");
            EnsureFolder("Assets/Scripts/Editor");
            EnsureFolder("Assets/Resources/Bootstrap");

            Sprite background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            font = LoadKenneyFont(font);
            background = FindKenneyUiSprite(background, "button_square_depth_flat", "button_square_flat");
            uiSprite = FindKenneyUiSprite(uiSprite, "button_round_depth_flat", "button_square_depth_flat");
            knob = FindKenneyUiSprite(knob, "slide_hangle");

            Sprite avatarSprite = FindKenneyUiSprite(uiSprite, "icon_circle", "icon_square");
            Sprite minimapSprite = FindKenneyUiSprite(background, "icon_square", "icon_circle");
            Sprite jumpSprite = FindKenneyUiSprite(uiSprite, "icon_checkmark", "icon_circle");
            Sprite dashSprite = FindKenneyUiSprite(uiSprite, "icon_square", "icon_circle");
            Sprite arrowSprite = FindKenneyUiSprite(uiSprite, "arrow_basic_e_small", "arrow_basic_e");
            Sprite musicSprite = FindKenneyUiSprite(uiSprite, "icon_circle", "icon_checkmark");

            if (avatarSprite == uiSprite)
            {
                avatarSprite = FindExternalSprite(uiSprite, "avatar", "profile", "character");
            }

            if (minimapSprite == background)
            {
                minimapSprite = FindExternalSprite(background, "minimap", "map");
            }

            GameObject runHudRoot = BuildRunHudRoot(
                background,
                uiSprite,
                knob,
                font,
                avatarSprite,
                minimapSprite,
                jumpSprite,
                dashSprite,
                arrowSprite);
            GameObject runHudPrefab = SavePrefab(runHudRoot, RunHudPrefabPath);

            GameObject modalRoot = BuildMusicSelectionModalRoot(
                background,
                uiSprite,
                font,
                musicSprite);
            GameObject modalPrefab = SavePrefab(modalRoot, MusicModalPrefabPath);

            GameObject runResultRoot = BuildRunResultModalRoot(background, uiSprite, font);
            GameObject runResultPrefab = SavePrefab(runResultRoot, RunResultModalPrefabPath);

            CreateOrUpdateCatalog();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DeliveryRunUiPrefabGenerator] Generated:\n - " +
                      RunHudPrefabPath + "\n - " +
                      MusicModalPrefabPath + "\n - " +
                      RunResultModalPrefabPath + "\n - " +
                      CatalogPath);
        }

        private static GameObject BuildRunHudRoot(
            Sprite background,
            Sprite uiSprite,
            Sprite knob,
            Font font,
            Sprite avatarSprite,
            Sprite minimapSprite,
            Sprite jumpSprite,
            Sprite dashSprite,
            Sprite arrowSprite)
        {
            GameObject root = CreateUiObject(
                "RunHUD",
                null,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchFull(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            ForceOverrideSorting(canvas, true);

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject safeArea = CreateUiObject("SafeArea", root.transform);
            StretchFull(safeArea.GetComponent<RectTransform>());

            Image avatarImage;
            Text cashText;
            Image topLeftBarFill;
            Image minimapImage;
            Slider boostSlider;
            Text boostText;
            Text buffText;
            Button jumpButton;
            Button dashButton;
            Text nowPlayingText;
            Text deliverToText;
            Text timeText;
            Button focusButton;

            CreateTopLeftPanel(
                safeArea.transform,
                background,
                uiSprite,
                font,
                avatarSprite,
                out avatarImage,
                out cashText,
                out topLeftBarFill);

            CreateTopRightMinimap(
                safeArea.transform,
                background,
                minimapSprite,
                out minimapImage);

            CreateBottomLeftStatus(
                safeArea.transform,
                background,
                uiSprite,
                knob,
                font,
                jumpSprite,
                dashSprite,
                out boostSlider,
                out boostText,
                out buffText,
                out jumpButton,
                out dashButton);

            CreateBottomCenterNowPlaying(
                safeArea.transform,
                background,
                font,
                out nowPlayingText);

            CreateBottomRightDelivery(
                safeArea.transform,
                background,
                uiSprite,
                font,
                arrowSprite,
                out deliverToText,
                out timeText,
                out focusButton);

            RunHudView view = root.AddComponent<RunHudView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("avatarImage").objectReferenceValue = avatarImage;
            so.FindProperty("cashText").objectReferenceValue = cashText;
            so.FindProperty("topLeftBarFill").objectReferenceValue = topLeftBarFill;
            so.FindProperty("minimapImage").objectReferenceValue = minimapImage;
            so.FindProperty("boostSlider").objectReferenceValue = boostSlider;
            so.FindProperty("boostText").objectReferenceValue = boostText;
            so.FindProperty("buffActiveText").objectReferenceValue = buffText;
            so.FindProperty("jumpButton").objectReferenceValue = jumpButton;
            so.FindProperty("dashButton").objectReferenceValue = dashButton;
            so.FindProperty("nowPlayingText").objectReferenceValue = nowPlayingText;
            so.FindProperty("deliverToText").objectReferenceValue = deliverToText;
            so.FindProperty("timeText").objectReferenceValue = timeText;
            so.FindProperty("focusButton").objectReferenceValue = focusButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject BuildMusicSelectionModalRoot(
            Sprite background,
            Sprite uiSprite,
            Font font,
            Sprite musicIcon)
        {
            GameObject root = CreateUiObject(
                "MusicSelectionModal",
                null,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchFull(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            ForceOverrideSorting(canvas, true);

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image dimBackground = CreateImage(
                "DimBackground",
                root.transform,
                null,
                new Color(0f, 0f, 0f, 0.35f),
                false);
            StretchFull(dimBackground.rectTransform);
            dimBackground.raycastTarget = true;
            dimBackground.transform.SetAsFirstSibling();

            Text titleText = CreateText(
                "TitleText",
                root.transform,
                font,
                "Music Selection",
                56,
                TextAnchor.MiddleCenter,
                new Color(0.96f, 0.96f, 0.96f, 1f));
            SetRect(
                titleText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -80f),
                new Vector2(700f, 100f));
            titleText.raycastTarget = false;

            GameObject cardRow = CreateUiObject("CardRow", root.transform, typeof(HorizontalLayoutGroup));
            RectTransform rowRect = cardRow.GetComponent<RectTransform>();
            SetRect(
                rowRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f),
                new Vector2(1500f, 520f));

            HorizontalLayoutGroup layout = cardRow.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Button[] optionButtons = new Button[3];
            Text[] optionTitleTexts = new Text[3];
            Text[] optionSubTexts = new Text[3];
            Text[] optionSynergyTexts = new Text[3];

            for (int i = 0; i < 3; i++)
            {
                CreateMusicOptionCard(
                    cardRow.transform,
                    background,
                    uiSprite,
                    font,
                    musicIcon,
                    i,
                    out optionButtons[i],
                    out optionTitleTexts[i],
                    out optionSubTexts[i],
                    out optionSynergyTexts[i]);
            }

            MusicSelectionModalView view = root.AddComponent<MusicSelectionModalView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("titleText").objectReferenceValue = titleText;

            SerializedProperty optionButtonsProp = so.FindProperty("optionButtons");
            optionButtonsProp.arraySize = optionButtons.Length;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                optionButtonsProp.GetArrayElementAtIndex(i).objectReferenceValue = optionButtons[i];
            }

            SerializedProperty optionTitleProp = so.FindProperty("optionTitleTexts");
            optionTitleProp.arraySize = optionTitleTexts.Length;
            for (int i = 0; i < optionTitleTexts.Length; i++)
            {
                optionTitleProp.GetArrayElementAtIndex(i).objectReferenceValue = optionTitleTexts[i];
            }

            SerializedProperty optionSubProp = so.FindProperty("optionSubTexts");
            optionSubProp.arraySize = optionSubTexts.Length;
            for (int i = 0; i < optionSubTexts.Length; i++)
            {
                optionSubProp.GetArrayElementAtIndex(i).objectReferenceValue = optionSubTexts[i];
            }

            SerializedProperty optionSynergyProp = so.FindProperty("optionSynergyTexts");
            optionSynergyProp.arraySize = optionSynergyTexts.Length;
            for (int i = 0; i < optionSynergyTexts.Length; i++)
            {
                optionSynergyProp.GetArrayElementAtIndex(i).objectReferenceValue = optionSynergyTexts[i];
            }

            so.FindProperty("closeButton").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject BuildRunResultModalRoot(Sprite background, Sprite uiSprite, Font font)
        {
            GameObject root = CreateUiObject(
                "RunResultModal",
                null,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchFull(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            ForceOverrideSorting(canvas, true);

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image dim = CreateImage(
                "DimBackground",
                root.transform,
                null,
                new Color(0f, 0f, 0f, 0.42f),
                false);
            StretchFull(dim.rectTransform);

            Image panel = CreateImage(
                "ResultPanel",
                root.transform,
                background,
                new Color(0.08f, 0.11f, 0.14f, 0.97f),
                true);
            SetRect(
                panel.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(560f, 360f));

            Text titleText = CreateText(
                "TitleText",
                panel.transform,
                font,
                "RUN RESULT",
                42,
                TextAnchor.MiddleCenter,
                Color.white);
            SetRect(
                titleText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -42f),
                new Vector2(-32f, 64f));

            Text summaryText = CreateText(
                "SummaryText",
                panel.transform,
                font,
                "Cash: $0\nOrders: 0\nMusic Option: -1",
                28,
                TextAnchor.UpperLeft,
                new Color(0.94f, 0.94f, 0.94f, 1f));
            SetRect(
                summaryText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f),
                new Vector2(-52f, -146f));

            Button okButton = CreateActionButton(
                "OkButton",
                panel.transform,
                uiSprite,
                null,
                font,
                "OK");
            SetRect(
                okButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 30f),
                new Vector2(180f, 48f));

            RunResultModalView view = root.AddComponent<RunResultModalView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("summaryText").objectReferenceValue = summaryText;
            so.FindProperty("okButton").objectReferenceValue = okButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void CreateTopLeftPanel(
            Transform parent,
            Sprite background,
            Sprite uiSprite,
            Font font,
            Sprite avatarSprite,
            out Image avatarImage,
            out Text cashText,
            out Image topLeftBarFill)
        {
            Image panel = CreateImage(
                "TopLeftPanel",
                parent,
                background,
                new Color(0.08f, 0.11f, 0.14f, 0.9f),
                true);
            SetRect(
                panel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(40f, -40f),
                new Vector2(420f, 90f));

            avatarImage = CreateImage(
                "Avatar",
                panel.transform,
                avatarSprite,
                Color.white,
                false);
            SetRect(
                avatarImage.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(12f, 0f),
                new Vector2(64f, 64f));

            cashText = CreateText(
                "CashText",
                panel.transform,
                font,
                "CASH: $1,250",
                30,
                TextAnchor.MiddleLeft,
                Color.white);
            SetRect(
                cashText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(92f, 8f),
                new Vector2(-104f, -24f));

            Image barBackground = CreateImage(
                "TopLeftBarBackground",
                panel.transform,
                uiSprite,
                new Color(0.1f, 0.15f, 0.21f, 1f),
                true);
            SetRect(
                barBackground.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(92f, 8f),
                new Vector2(-110f, 12f));

            topLeftBarFill = CreateImage(
                "TopLeftBarFill",
                barBackground.transform,
                uiSprite,
                new Color(0.22f, 0.85f, 0.65f, 1f),
                true);
            StretchFull(topLeftBarFill.rectTransform);
            topLeftBarFill.type = Image.Type.Filled;
            topLeftBarFill.fillMethod = Image.FillMethod.Horizontal;
            topLeftBarFill.fillOrigin = 0;
            topLeftBarFill.fillAmount = 0.72f;
        }

        private static void CreateTopRightMinimap(
            Transform parent,
            Sprite background,
            Sprite minimapSprite,
            out Image minimapImage)
        {
            Image panel = CreateImage(
                "TopRightMiniMap",
                parent,
                background,
                new Color(0.08f, 0.11f, 0.14f, 0.9f),
                true);
            SetRect(
                panel.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-40f, -40f),
                new Vector2(220f, 120f));

            minimapImage = CreateImage(
                "MapImage",
                panel.transform,
                minimapSprite,
                new Color(0.95f, 0.95f, 0.95f, 1f),
                false);
            SetRect(
                minimapImage.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-16f, -16f));
        }

        private static void CreateBottomLeftStatus(
            Transform parent,
            Sprite background,
            Sprite uiSprite,
            Sprite knob,
            Font font,
            Sprite jumpIcon,
            Sprite dashIcon,
            out Slider boostSlider,
            out Text boostText,
            out Text buffText,
            out Button jumpButton,
            out Button dashButton)
        {
            Image panel = CreateImage(
                "BottomLeftStatus",
                parent,
                background,
                new Color(0.08f, 0.11f, 0.14f, 0.9f),
                true);
            SetRect(
                panel.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(40f, 40f),
                new Vector2(280f, 180f));

            boostText = CreateText(
                "BoostLabel",
                panel.transform,
                font,
                "BOOST: 80%",
                22,
                TextAnchor.MiddleLeft,
                Color.white);
            SetRect(
                boostText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -16f),
                new Vector2(-20f, 32f));

            boostSlider = CreateSlider("BoostSlider", panel.transform, uiSprite, knob);
            SetRect(
                boostSlider.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -56f),
                new Vector2(-24f, 20f));
            boostSlider.value = 0.8f;

            buffText = CreateText(
                "BuffLabel",
                panel.transform,
                font,
                "BUFF ACTIVE: NITRO BEAT (+20% SPD)",
                16,
                TextAnchor.MiddleLeft,
                new Color(0.89f, 0.95f, 1f, 1f));
            SetRect(
                buffText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -90f),
                new Vector2(-20f, 30f));

            jumpButton = CreateActionButton(
                "JumpButton",
                panel.transform,
                uiSprite,
                jumpIcon,
                font,
                "JUMP");
            SetRect(
                jumpButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(14f, 12f),
                new Vector2(120f, 36f));

            dashButton = CreateActionButton(
                "DashButton",
                panel.transform,
                uiSprite,
                dashIcon,
                font,
                "DASH");
            SetRect(
                dashButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(146f, 12f),
                new Vector2(120f, 36f));
        }

        private static void CreateBottomCenterNowPlaying(
            Transform parent,
            Sprite background,
            Font font,
            out Text nowPlayingText)
        {
            Image panel = CreateImage(
                "BottomCenterNowPlaying",
                parent,
                background,
                new Color(0.08f, 0.11f, 0.14f, 0.9f),
                true);
            SetRect(
                panel.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 40f),
                new Vector2(360f, 56f));

            nowPlayingText = CreateText(
                "NowPlayingText",
                panel.transform,
                font,
                "NOW PLAYING: NITRO BOOST BEAT",
                18,
                TextAnchor.MiddleCenter,
                new Color(0.95f, 0.95f, 0.95f, 1f));
            StretchFull(nowPlayingText.rectTransform);
        }

        private static void CreateBottomRightDelivery(
            Transform parent,
            Sprite background,
            Sprite uiSprite,
            Font font,
            Sprite arrowSprite,
            out Text deliverToText,
            out Text timeText,
            out Button focusButton)
        {
            Image panel = CreateImage(
                "BottomRightDeliveryInfo",
                parent,
                background,
                new Color(0.08f, 0.11f, 0.14f, 0.9f),
                true);
            SetRect(
                panel.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-40f, 40f),
                new Vector2(320f, 96f));

            deliverToText = CreateText(
                "DeliverToText",
                panel.transform,
                font,
                "DELIVER TO: NEO-TOKYO TOWER",
                18,
                TextAnchor.UpperLeft,
                Color.white);
            SetRect(
                deliverToText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -12f),
                new Vector2(-80f, 38f));

            timeText = CreateText(
                "TimeText",
                panel.transform,
                font,
                "(Time: 02:45)",
                16,
                TextAnchor.LowerLeft,
                new Color(0.86f, 0.93f, 1f, 1f));
            SetRect(
                timeText.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(14f, 10f),
                new Vector2(-80f, 26f));

            focusButton = CreateIconButton(
                "FocusButton",
                panel.transform,
                uiSprite,
                arrowSprite);
            SetRect(
                focusButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f),
                new Vector2(44f, 44f));
        }

        private static void CreateMusicOptionCard(
            Transform parent,
            Sprite background,
            Sprite uiSprite,
            Font font,
            Sprite iconSprite,
            int index,
            out Button selectButton,
            out Text mainTitleText,
            out Text subTitleText,
            out Text synergyText)
        {
            Image card = CreateImage(
                "Card" + (index + 1),
                parent,
                background,
                new Color(0.96f, 0.96f, 0.98f, 0.96f),
                true);
            card.raycastTarget = false;
            RectTransform cardRect = card.rectTransform;
            cardRect.sizeDelta = new Vector2(420f, 520f);

            LayoutElement layoutElement = card.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 420f;
            layoutElement.preferredHeight = 520f;
            layoutElement.flexibleWidth = 0f;

            Image header = CreateImage(
                "HeaderStrip",
                card.transform,
                uiSprite,
                new Color(0.08f, 0.12f, 0.2f, 0.95f),
                true);
            header.raycastTarget = false;
            SetRect(
                header.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 70f));

            mainTitleText = CreateText(
                "MainTitle",
                card.transform,
                font,
                "Option " + (index + 1),
                34,
                TextAnchor.MiddleCenter,
                new Color(0.06f, 0.09f, 0.16f, 1f));
            SetRect(
                mainTitleText.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 36f),
                new Vector2(-30f, 72f));

            subTitleText = CreateText(
                "SubTitle",
                card.transform,
                font,
                "Tempo and style change",
                22,
                TextAnchor.MiddleCenter,
                new Color(0.11f, 0.13f, 0.18f, 1f));
            SetRect(
                subTitleText.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f),
                new Vector2(-36f, 46f));

            Image icon = CreateImage(
                "SmallIconRightTop",
                card.transform,
                iconSprite,
                Color.white,
                false);
            icon.raycastTarget = false;
            SetRect(
                icon.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-16f, -16f),
                new Vector2(36f, 36f));

            synergyText = CreateText(
                "SynergyText",
                card.transform,
                font,
                "Synergy: +15% Combo Gain",
                19,
                TextAnchor.UpperCenter,
                new Color(0.09f, 0.12f, 0.2f, 1f));
            SetRect(
                synergyText.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f),
                new Vector2(-40f, 210f));

            selectButton = CreateActionButton(
                "CardSelectButton",
                card.transform,
                uiSprite,
                null,
                font,
                string.Empty);
            SetRect(
                selectButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                Vector2.zero);

            Image cardSelectImage = selectButton.GetComponent<Image>();
            if (cardSelectImage != null)
            {
                cardSelectImage.color = new Color(1f, 1f, 1f, 0f);
                cardSelectImage.raycastTarget = true;
            }

            Transform label = selectButton.transform.Find("Label");
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }
        }

        private static Slider CreateSlider(string name, Transform parent, Sprite uiSprite, Sprite knob)
        {
            GameObject sliderObj = CreateUiObject(name, parent, typeof(Slider));
            Slider slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            Image background = CreateImage(
                "Background",
                sliderObj.transform,
                uiSprite,
                new Color(0.11f, 0.14f, 0.19f, 1f),
                true);
            StretchFull(background.rectTransform);

            GameObject fillArea = CreateUiObject("Fill Area", sliderObj.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(8f, 5f);
            fillAreaRect.offsetMax = new Vector2(-8f, -5f);

            Image fill = CreateImage(
                "Fill",
                fillArea.transform,
                uiSprite,
                new Color(0.26f, 0.82f, 0.66f, 1f),
                true);
            StretchFull(fill.rectTransform);

            GameObject handleSlideArea = CreateUiObject("Handle Slide Area", sliderObj.transform);
            RectTransform handleSlideRect = handleSlideArea.GetComponent<RectTransform>();
            handleSlideRect.anchorMin = new Vector2(0f, 0f);
            handleSlideRect.anchorMax = new Vector2(1f, 1f);
            handleSlideRect.offsetMin = new Vector2(8f, 0f);
            handleSlideRect.offsetMax = new Vector2(-8f, 0f);

            Image handle = CreateImage(
                "Handle",
                handleSlideArea.transform,
                knob,
                Color.white,
                false);
            RectTransform handleRect = handle.rectTransform;
            SetRect(
                handleRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(20f, 20f));

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;

            return slider;
        }

        private static Button CreateActionButton(
            string name,
            Transform parent,
            Sprite uiSprite,
            Sprite iconSprite,
            Font font,
            string label)
        {
            Image image = CreateImage(
                name,
                parent,
                uiSprite,
                new Color(0.13f, 0.17f, 0.25f, 1f),
                true);
            Button button = image.gameObject.AddComponent<Button>();

            if (iconSprite != null)
            {
                Image icon = CreateImage(
                    "Icon",
                    image.transform,
                    iconSprite,
                    Color.white,
                    false);
                SetRect(
                    icon.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(10f, 0f),
                    new Vector2(18f, 18f));
            }

            Text text = CreateText(
                "Label",
                image.transform,
                font,
                label,
                17,
                TextAnchor.MiddleCenter,
                Color.white);
            SetRect(
                text.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                iconSprite != null ? new Vector2(-12f, 0f) : Vector2.zero);

            return button;
        }

        private static Button CreateIconButton(
            string name,
            Transform parent,
            Sprite uiSprite,
            Sprite iconSprite)
        {
            Image image = CreateImage(
                name,
                parent,
                uiSprite,
                new Color(0.13f, 0.17f, 0.25f, 1f),
                true);
            Button button = image.gameObject.AddComponent<Button>();

            Image icon = CreateImage(
                "Icon",
                image.transform,
                iconSprite,
                Color.white,
                false);
            SetRect(
                icon.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(20f, 20f));

            return button;
        }

        private static void CreateOrUpdateCatalog()
        {
            UiPrefabCatalogSO catalog = AssetDatabase.LoadAssetAtPath<UiPrefabCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UiPrefabCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.RunHudPrefab = null;
            catalog.MusicSelectionModalPrefab = null;
            catalog.RunResultModalPrefab = null;
            catalog.RunHudKey = "ui/run/hud";
            catalog.MusicSelectionModalKey = "ui/run/music_selection_modal";
            catalog.RunResultModalKey = "ui/run/run_result_modal";
            catalog.UiRunLabel = "ui:run";
            catalog.TestBgmKey = "audio/bgm/test_bgm";
            catalog.UiClickKey = "audio/ui/click";
            EditorUtility.SetDirty(catalog);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
            {
                throw new UnityException("Failed to save prefab: " + path);
            }

            return prefab;
        }

        private static Font LoadKenneyFont(Font fallback)
        {
            if (!AssetDatabase.IsValidFolder(KenneyUiFontFolder))
            {
                return fallback;
            }

            string[] preferred =
            {
                KenneyUiFontFolder + "/Kenney Future.ttf",
                KenneyUiFontFolder + "/Kenney Future Narrow.ttf"
            };

            for (int i = 0; i < preferred.Length; i++)
            {
                Font loaded = AssetDatabase.LoadAssetAtPath<Font>(preferred[i]);
                if (loaded != null)
                {
                    return loaded;
                }
            }

            return fallback;
        }

        private static Sprite FindKenneyUiSprite(Sprite fallback, params string[] keywords)
        {
            if (!AssetDatabase.IsValidFolder(KenneyUiPngFolder) || keywords == null)
            {
                return fallback;
            }

            string[] folders = { KenneyUiPngFolder };
            Sprite best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Sprite " + keyword, folders);
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        continue;
                    }

                    string lowered = path.Replace("\\", "/").ToLowerInvariant();
                    int score = 0;
                    if (lowered.Contains("/blue/default/")) score += 40;
                    else if (lowered.Contains("/blue/")) score += 30;
                    else if (lowered.Contains("/default/")) score += 20;
                    if (lowered.Contains(keyword.ToLowerInvariant())) score += 10;

                    if (score > bestScore)
                    {
                        best = sprite;
                        bestScore = score;
                    }
                }
            }

            return best != null ? best : fallback;
        }

        private static Sprite FindExternalSprite(Sprite fallback, params string[] keywords)
        {
            if (!AssetDatabase.IsValidFolder(ExternalsFolder) || keywords == null)
            {
                return fallback;
            }

            string[] searchFolders = { ExternalsFolder };
            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Sprite " + keyword, searchFolders);
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }

            return fallback;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] extraComponents)
        {
            int extraCount = extraComponents != null ? extraComponents.Length : 0;
            Type[] componentTypes = new Type[extraCount + 1];
            componentTypes[0] = typeof(RectTransform);
            for (int i = 0; i < extraCount; i++)
            {
                componentTypes[i + 1] = extraComponents[i];
            }

            GameObject go = new GameObject(name, componentTypes);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            bool sliced)
        {
            GameObject go = CreateUiObject(name, parent, typeof(Image));
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = true;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string text,
            int fontSize,
            TextAnchor anchor,
            Color color)
        {
            GameObject go = CreateUiObject(name, parent, typeof(Text));
            Text textComp = go.GetComponent<Text>();
            textComp.font = font;
            textComp.text = text;
            textComp.fontSize = fontSize;
            textComp.alignment = anchor;
            textComp.color = color;
            textComp.raycastTarget = false;
            textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComp.verticalOverflow = VerticalWrapMode.Overflow;
            return textComp;
        }

        private static void SetRect(
            RectTransform rt,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            if (parts.Length == 0)
            {
                return;
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void ForceOverrideSorting(Canvas canvas, bool enabled)
        {
            canvas.overrideSorting = enabled;
            SerializedObject so = new SerializedObject(canvas);
            SerializedProperty property = so.FindProperty("m_OverrideSorting");
            if (property != null)
            {
                property.boolValue = enabled;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
