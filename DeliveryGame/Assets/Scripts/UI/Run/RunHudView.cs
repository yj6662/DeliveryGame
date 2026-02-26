using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed class RunHudView : MonoBehaviour
    {
        [Header("Top Left")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text cashText;
        [SerializeField] private Image topLeftBarFill;

        [Header("Top Right")]
        [SerializeField] private Image minimapImage;

        [Header("Bottom Left")]
        [SerializeField] private Slider boostSlider;
        [SerializeField] private Text boostText;
        [SerializeField] private Text buffActiveText;
        [SerializeField] private Button jumpButton;
        [SerializeField] private Button dashButton;

        [Header("Bottom Center")]
        [SerializeField] private Text nowPlayingText;

        [Header("Bottom Right")]
        [SerializeField] private Text deliverToText;
        [SerializeField] private Text timeText;
        [SerializeField] private Button focusButton;

        public void SetCash(string text)
        {
            if (cashText != null)
            {
                cashText.text = text ?? string.Empty;
            }
        }

        public void SetBoost01(float v01)
        {
            if (boostSlider != null)
            {
                boostSlider.value = Mathf.Clamp01(v01);
            }
        }

        public void SetBoostLabel(string text)
        {
            if (boostText != null)
            {
                boostText.text = text ?? string.Empty;
            }
        }

        public void SetBuffLabel(string text)
        {
            if (buffActiveText != null)
            {
                buffActiveText.text = text ?? string.Empty;
            }
        }

        public void SetNowPlaying(string text)
        {
            if (nowPlayingText != null)
            {
                nowPlayingText.text = text ?? string.Empty;
            }
        }

        public void SetDelivery(string text)
        {
            if (deliverToText != null)
            {
                deliverToText.text = text ?? string.Empty;
            }
        }

        public void SetTimeLabel(string text)
        {
            if (timeText != null)
            {
                timeText.text = text ?? string.Empty;
            }
        }

        public void SetFocusButtonAction(UnityAction onClick)
        {
            if (focusButton == null)
            {
                return;
            }

            focusButton.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                focusButton.onClick.AddListener(onClick);
            }
        }

        public RectTransform GetMinimapRectTransform()
        {
            return minimapImage != null ? minimapImage.rectTransform : null;
        }

        public Image GetMinimapImage()
        {
            return minimapImage;
        }

        public RectTransform GetMinimapPanelRectTransform()
        {
            return minimapImage != null ? minimapImage.transform.parent as RectTransform : null;
        }

        public RectTransform GetDeliveryPanelRectTransform()
        {
            return deliverToText != null ? deliverToText.transform.parent as RectTransform : null;
        }

        public RectTransform GetRootRectTransform()
        {
            return transform as RectTransform;
        }

        public Font GetDefaultFont()
        {
            if (cashText != null && cashText.font != null)
            {
                return cashText.font;
            }

            if (nowPlayingText != null && nowPlayingText.font != null)
            {
                return nowPlayingText.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public Sprite GetPanelSkinSprite()
        {
            RectTransform panel = GetMinimapPanelRectTransform();
            if (panel == null)
            {
                return null;
            }

            Image panelImage = panel.GetComponent<Image>();
            return panelImage != null ? panelImage.sprite : null;
        }

        public void SetDeliveryPanelVisible(bool visible)
        {
            RectTransform panel = GetDeliveryPanelRectTransform();
            if (panel == null)
            {
                return;
            }

            if (panel.gameObject.activeSelf != visible)
            {
                panel.gameObject.SetActive(visible);
            }
        }

        public void ConfigureStatusHudCompact()
        {
            if (boostSlider != null)
            {
                boostSlider.gameObject.SetActive(false);
            }

            if (jumpButton != null)
            {
                jumpButton.gameObject.SetActive(false);
            }

            if (dashButton != null)
            {
                dashButton.gameObject.SetActive(false);
            }
        }

        public void SetStatusSpeed(string text)
        {
            SetBoostLabel(text);
        }

        public void SetStatusBuffAndSynergy(string buffText, string synergyText)
        {
            if (buffActiveText == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(buffText))
            {
                buffText = "BUFF: -";
            }

            if (string.IsNullOrEmpty(synergyText))
            {
                synergyText = "SYNERGY: -";
            }

            buffActiveText.text = buffText + "\n" + synergyText;
        }
    }
}
