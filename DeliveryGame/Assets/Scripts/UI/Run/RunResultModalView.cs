using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed class RunResultModalView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text summaryText;
        [SerializeField] private Button okButton;

        private Action _onOk;

        public void Show(string summary, Action onOk)
        {
            _onOk = onOk;

            if (summaryText != null)
            {
                summaryText.text = summary ?? string.Empty;
            }

            if (okButton != null)
            {
                okButton.onClick.RemoveListener(OnOkClicked);
                okButton.onClick.AddListener(OnOkClicked);
            }

            SetVisible(true);
        }

        public void Hide()
        {
            if (okButton != null)
            {
                okButton.onClick.RemoveListener(OnOkClicked);
            }

            SetVisible(false);
        }

        private void OnOkClicked()
        {
            Action callback = _onOk;
            _onOk = null;
            Hide();
            if (callback != null)
            {
                callback();
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
