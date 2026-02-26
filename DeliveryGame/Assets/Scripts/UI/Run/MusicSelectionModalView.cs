using System;
using UnityEngine;
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

        private void Awake()
        {
            EnsureCanvasRaycaster();
            EnsureSiblingOrder();
            Hide();
        }

        public void Show(Action<int> onSelect)
        {
            _onSelect = onSelect;
            EnsureCanvasRaycaster();
            EnsureSiblingOrder();
            BindButtonsForShow();

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

        private void BindButtonsForShow()
        {
            int buttonCount = optionButtons != null ? optionButtons.Length : 0;
            for (int i = 0; i < buttonCount; i++)
            {
                int idx = i;
                Button button = optionButtons[idx];
                if (button == null)
                {
                    continue;
                }

                button.interactable = true;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("[MusicSelectionModalView] Select " + idx);
#endif
                    _onSelect?.Invoke(idx);
                });
            }

            if (closeButton != null)
            {
                closeButton.interactable = true;
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void EnsureSiblingOrder()
        {
            Transform dim = transform.Find("DimBackground");
            if (dim != null)
            {
                dim.SetAsFirstSibling();
            }

            Transform cardRow = transform.Find("CardRow");
            if (cardRow != null)
            {
                cardRow.SetAsLastSibling();
            }
        }

        private void EnsureCanvasRaycaster()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }
}
