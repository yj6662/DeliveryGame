using System;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudPausePanel
    {
        private RectTransform _pauseRootRect;
        private GameObject _pausePanel;
        private bool _pauseOpen;
        private bool _pauseCaptured;
        private float _pauseSavedScale;

        internal bool IsOpen => _pauseOpen;

        internal void BuildIfNeeded(RunHudView view, Action onBackLobbyRequested, Action onUiClick)
        {
            if (view == null)
            {
                return;
            }

            RectTransform root = view.GetRootRectTransform();
            if (root == null || _pauseRootRect != null)
            {
                return;
            }

            GameObject pauseRoot = new GameObject("PauseUiRoot", typeof(RectTransform));
            _pauseRootRect = pauseRoot.GetComponent<RectTransform>();
            _pauseRootRect.SetParent(root, false);
            _pauseRootRect.anchorMin = new Vector2(1f, 1f);
            _pauseRootRect.anchorMax = new Vector2(1f, 1f);
            _pauseRootRect.pivot = new Vector2(1f, 1f);
            _pauseRootRect.anchoredPosition = new Vector2(-20f, -20f);
            _pauseRootRect.sizeDelta = new Vector2(252f, 212f);

            Font font = view.GetDefaultFont();
            Sprite panelSkin = view.GetPanelSkinSprite();

            Button pauseButton = HudUiFactory.CreateButton("PauseButton", _pauseRootRect, font, panelSkin, "Pause", () =>
            {
                onUiClick?.Invoke();
                Toggle();
            });
            RectTransform pauseButtonRect = pauseButton.transform as RectTransform;
            pauseButtonRect.anchorMin = new Vector2(1f, 1f);
            pauseButtonRect.anchorMax = new Vector2(1f, 1f);
            pauseButtonRect.pivot = new Vector2(1f, 1f);
            pauseButtonRect.anchoredPosition = new Vector2(-10f, -10f);
            pauseButtonRect.sizeDelta = new Vector2(120f, 36f);

            GameObject panel = new GameObject("PausePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _pausePanel = panel;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(_pauseRootRect, false);
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-10f, -56f);
            panelRect.sizeDelta = new Vector2(220f, 130f);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = panelSkin;
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            Button resumeButton = HudUiFactory.CreateButton("ResumeButton", panelRect, font, panelSkin, "Resume", () =>
            {
                onUiClick?.Invoke();
                Close(true);
            });
            RectTransform resumeRect = resumeButton.transform as RectTransform;
            resumeRect.anchorMin = new Vector2(0.5f, 1f);
            resumeRect.anchorMax = new Vector2(0.5f, 1f);
            resumeRect.pivot = new Vector2(0.5f, 1f);
            resumeRect.anchoredPosition = new Vector2(0f, -42f);
            resumeRect.sizeDelta = new Vector2(140f, 34f);

            Button lobbyButton = HudUiFactory.CreateButton("LobbyButton", panelRect, font, panelSkin, "Back Lobby", () =>
            {
                onUiClick?.Invoke();
                Close(true);
                onBackLobbyRequested?.Invoke();
            });
            RectTransform lobbyRect = lobbyButton.transform as RectTransform;
            lobbyRect.anchorMin = new Vector2(0.5f, 1f);
            lobbyRect.anchorMax = new Vector2(0.5f, 1f);
            lobbyRect.pivot = new Vector2(0.5f, 1f);
            lobbyRect.anchoredPosition = new Vector2(0f, -82f);
            lobbyRect.sizeDelta = new Vector2(140f, 34f);

            _pausePanel.SetActive(false);
        }

        internal void Toggle()
        {
            if (_pauseOpen)
            {
                Close(true);
            }
            else
            {
                Open();
            }
        }

        internal void Open()
        {
            if (_pausePanel == null || _pauseOpen)
            {
                return;
            }

            _pauseOpen = true;
            _pausePanel.SetActive(true);

            if (!_pauseCaptured)
            {
                _pauseSavedScale = Time.timeScale;
                _pauseCaptured = true;
            }

            Time.timeScale = 0f;
        }

        internal void Close(bool restore)
        {
            _pauseOpen = false;
            if (_pausePanel != null)
            {
                _pausePanel.SetActive(false);
            }

            if (restore && _pauseCaptured)
            {
                Time.timeScale = _pauseSavedScale;
                _pauseSavedScale = 0f;
                _pauseCaptured = false;
            }
        }

        internal void Cleanup()
        {
            Close(true);
            _pauseRootRect = null;
            _pausePanel = null;
        }
    }
}
