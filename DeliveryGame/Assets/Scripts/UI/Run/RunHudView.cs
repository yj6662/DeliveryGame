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

        public void SetTopLeftBarFill01(float value01)
        {
            if (topLeftBarFill != null)
            {
                topLeftBarFill.fillAmount = Mathf.Clamp01(value01);
            }
        }

        public void SetTopLeftBarColor(Color color)
        {
            if (topLeftBarFill != null)
            {
                topLeftBarFill.color = color;
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

        public RectTransform GetBottomLeftPanelRectTransform()
        {
            return boostText != null ? boostText.transform.parent as RectTransform : null;
        }

        public RectTransform GetBottomCenterPanelRectTransform()
        {
            return nowPlayingText != null ? nowPlayingText.transform.parent as RectTransform : null;
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

        public void SetBottomLeftPanelVisible(bool visible)
        {
            RectTransform panel = GetBottomLeftPanelRectTransform();
            if (panel == null)
            {
                return;
            }

            if (panel.gameObject.activeSelf != visible)
            {
                panel.gameObject.SetActive(visible);
            }
        }

        public void ConfigureCenterStatusMerged()
        {
            RectTransform panel = GetBottomCenterPanelRectTransform();
            if (panel != null)
            {
                panel.sizeDelta = new Vector2(520f, 108f);
                panel.anchoredPosition = new Vector2(0f, 20f);

                Image panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0.09f, 0.12f, 0.16f, 0.9f);
                    panelImage.raycastTarget = false;
                }
            }

            if (nowPlayingText != null)
            {
                nowPlayingText.alignment = TextAnchor.UpperLeft;
                nowPlayingText.fontSize = 14;
                nowPlayingText.lineSpacing = 1.08f;
                nowPlayingText.horizontalOverflow = HorizontalWrapMode.Wrap;
                nowPlayingText.verticalOverflow = VerticalWrapMode.Overflow;
                nowPlayingText.raycastTarget = false;

                RectTransform textRect = nowPlayingText.rectTransform;
                textRect.anchorMin = new Vector2(0f, 0f);
                textRect.anchorMax = new Vector2(1f, 1f);
                textRect.offsetMin = new Vector2(16f, 10f);
                textRect.offsetMax = new Vector2(-16f, -10f);
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
