using UnityEngine;
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
    }
}
