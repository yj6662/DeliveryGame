using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed class MusicSelectionModalView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Text[] optionTitleTexts;
        [SerializeField] private Text[] optionSubTexts;
        [SerializeField] private Text[] optionSynergyTexts;
        [SerializeField] private Button closeButton;

        private Action<int> _onSelect;
        private UnityAction[] _cachedOptionHandlers;
        private UnityAction _cachedCloseHandler;

        private void Awake()
        {
            BindButtons();
            Hide();
        }

        public void Show(Action<int> onSelect)
        {
            _onSelect = onSelect;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void SetOption(int i, string title, string sub, string synergy)
        {
            if (i < 0)
            {
                return;
            }

            if (optionTitleTexts != null && i < optionTitleTexts.Length && optionTitleTexts[i] != null)
            {
                optionTitleTexts[i].text = title ?? string.Empty;
            }

            if (optionSubTexts != null && i < optionSubTexts.Length && optionSubTexts[i] != null)
            {
                optionSubTexts[i].text = sub ?? string.Empty;
            }

            if (optionSynergyTexts != null && i < optionSynergyTexts.Length && optionSynergyTexts[i] != null)
            {
                optionSynergyTexts[i].text = synergy ?? string.Empty;
            }
        }

        private void BindButtons()
        {
            int buttonCount = optionButtons != null ? optionButtons.Length : 0;
            if (buttonCount > 0)
            {
                if (_cachedOptionHandlers == null || _cachedOptionHandlers.Length != buttonCount)
                {
                    _cachedOptionHandlers = new UnityAction[buttonCount];
                }

                for (int i = 0; i < buttonCount; i++)
                {
                    Button button = optionButtons[i];
                    if (button == null)
                    {
                        continue;
                    }

                    if (_cachedOptionHandlers[i] != null)
                    {
                        button.onClick.RemoveListener(_cachedOptionHandlers[i]);
                    }

                    int index = i;
                    UnityAction handler = () => HandleOptionSelected(index);
                    _cachedOptionHandlers[i] = handler;
                    button.onClick.AddListener(handler);
                }
            }

            if (closeButton == null)
            {
                return;
            }

            if (_cachedCloseHandler == null)
            {
                _cachedCloseHandler = Hide;
            }
            else
            {
                closeButton.onClick.RemoveListener(_cachedCloseHandler);
            }

            closeButton.onClick.AddListener(_cachedCloseHandler);
        }

        private void HandleOptionSelected(int index)
        {
            Action<int> callback = _onSelect;
            if (callback != null)
            {
                callback(index);
            }
        }
    }
}
