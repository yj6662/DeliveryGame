using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class LobbyHubRuntime
    {
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
            _mainPanel = null;
            _mainTitleText = null;
            _homeNavButton = null;
            _garageNavButton = null;
            _regionNavButton = null;
            _homePanel = null;
            _homeSummaryText = null;
            _homeStartButton = null;
            _garagePanel = null;
            _upgradeHoverText = null;
            _regionPanel = null;
            _regionText = null;
            _regionDetailText = null;
            _unlockButton = null;
            _unlockButtonText = null;
            _nextRegionButton = null;
            _pendingUnlockRegionId = null;

            for (int i = 0; i < _upgradeRows.Length; i++)
            {
                _upgradeRows[i] = null;
                _upgradeButtons[i] = null;
            }

            for (int i = 0; i < _regionItemButtons.Length; i++)
            {
                _regionItemButtons[i] = null;
                _regionItemTexts[i] = null;
            }

            if (_uiRoot != null)
            {
                Object.Destroy(_uiRoot);
                _uiRoot = null;
            }

            ReleaseUiSkins();
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
                image.color = new Color(0.94f, 0.95f, 0.98f, 0.98f);
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
                bg.color = accent ? new Color(0.93f, 0.76f, 0.38f, 0.98f) : new Color(0.9f, 0.92f, 0.96f, 1f);
            }

            Button b = go.GetComponent<Button>();
            ColorBlock colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.87f, 0.87f, 0.93f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.62f);
            b.colors = colors;

            Text t = CreateText(
                rt,
                font,
                18,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Color(0.08f, 0.08f, 0.1f, 1f),
                label);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            return b;
        }

        private static void AddButtonHoverEvents(Button button, Action onEnter, Action onExit)
        {
            if (button == null)
            {
                return;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            if (trigger.triggers == null)
            {
                trigger.triggers = new List<EventTrigger.Entry>();
            }

            if (onEnter != null)
            {
                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback = new EventTrigger.TriggerEvent();
                entry.callback.AddListener(_ => onEnter());
                trigger.triggers.Add(entry);
            }

            if (onExit != null)
            {
                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                entry.callback = new EventTrigger.TriggerEvent();
                entry.callback.AddListener(_ => onExit());
                trigger.triggers.Add(entry);
            }
        }

        private static void SetButtonSelectedState(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(0.2f, 0.44f, 0.86f, 0.98f)
                    : Color.white;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = selected
                    ? Color.white
                    : new Color(0.08f, 0.08f, 0.1f, 1f);
            }
        }
    }
}
