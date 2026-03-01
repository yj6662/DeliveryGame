using System;
using DeliveryRun.Delivery.Input;
using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Run
{
    public sealed partial class MusicSelectionModalView : MonoBehaviour
    {
        private const float SelectedCardScale = 1.06f;
        private const float UnselectedCardScale = 0.95f;
        private const float ScaleLerpSpeed = 14f;
        private const float TextMoveLerpSpeed = 12f;
        private const float DetailFadeSpeed = 10f;
        private const float TierColorLerpSpeed = 10f;
        private const float GlowPulseSpeed = 3.2f;
        private const float HoverResumePointerMovePixels = 4f;

        private const float UnselectedTitleY = 36f;
        private const float UnselectedSubY = -6f;
        private const float SelectedTitleY = 170f;
        private const float SelectedSubY = 132f;
        private const float DetailTextY = -24f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Text[] optionTitleTexts;
        [SerializeField] private Text[] optionSubTexts;
        [SerializeField] private Text[] optionSynergyTexts;
        [SerializeField] private Button closeButton;

        private Action<int> _onSelect;

        private readonly string[] _optionDetailTexts = new string[3];
        private readonly string[] _optionSubDetailTexts = new string[3];
        private readonly RectTransform[] _optionCardRects = new RectTransform[3];
        private readonly RectTransform[] _titleRects = new RectTransform[3];
        private readonly RectTransform[] _subtitleRects = new RectTransform[3];
        private readonly Image[] _cardImages = new Image[3];
        private readonly Image[] _headerImages = new Image[3];
        private readonly Image[] _glowBorders = new Image[3];
        private readonly float[] _targetScales = new float[3];
        private readonly float[] _titleCurrentY = new float[3];
        private readonly float[] _subtitleCurrentY = new float[3];
        private readonly float[] _targetTitleY = new float[3];
        private readonly float[] _targetSubtitleY = new float[3];
        private readonly float[] _detailCurrentAlpha = new float[3];
        private readonly float[] _targetDetailAlpha = new float[3];
        private readonly int[] _tierCodes = new int[3];
        private readonly bool[] _immediateSynergy = new bool[3];

        private int _selectedIndex = -1;
        private bool _shown;
        private bool _suppressHoverUntilPointerMove;
        private Vector2 _pointerAtKeySelect;

        public bool IsReadyForSelectionForTests
        {
            get { return _shown && _onSelect != null; }
        }

        private void Awake()
        {
            EnsureCanvasRaycaster();
            EnsureSiblingOrder();
            ConfigureTitleTextLayout();
            CacheCardRects();
            Hide();
        }

        public void Show(Action<int> onSelect)
        {
            _onSelect = onSelect;
            EnsureCanvasRaycaster();
            EnsureSiblingOrder();
            ConfigureTitleTextLayout();
            CacheCardRects();
            BindInteractiveRelays();
            SetSelectedIndex(0, true);

            _suppressHoverUntilPointerMove = false;
            _pointerAtKeySelect = Vector2.zero;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            _shown = true;
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _shown = false;
        }

        public void SetOption(int i, string title, string sub, string detail)
        {
            if (i < 0 || i >= 3)
            {
                return;
            }

            _optionSubDetailTexts[i] = sub ?? string.Empty;
            _optionDetailTexts[i] = detail ?? string.Empty;

            if (optionTitleTexts != null && i < optionTitleTexts.Length && optionTitleTexts[i] != null)
            {
                optionTitleTexts[i].text = NormalizeTitleSingleLine(title);
            }

            if (optionSubTexts != null && i < optionSubTexts.Length && optionSubTexts[i] != null)
            {
                optionSubTexts[i].text = _optionSubDetailTexts[i];
            }

            if (optionSynergyTexts != null && i < optionSynergyTexts.Length && optionSynergyTexts[i] != null)
            {
                optionSynergyTexts[i].text = _optionDetailTexts[i];
            }

            ApplyDetailAlpha(i, _detailCurrentAlpha[i]);
        }

        public void SetOptionVisual(int i, int tierCode, bool immediateSynergy)
        {
            if (i < 0 || i >= 3)
            {
                return;
            }

            _tierCodes[i] = tierCode;
            _immediateSynergy[i] = immediateSynergy;
            ApplyCardThemeImmediate(i);
        }

        public bool ConfirmSelectionForTests(int index)
        {
            if (!_shown || _onSelect == null)
            {
                return false;
            }

            SetSelectedIndex(index, false);
            ConfirmSelection(index);
            return true;
        }

        private void Update()
        {
            if (!_shown)
            {
                return;
            }

            int keyIndex = RuntimeInput.ReadMusicOptionIndexPressedThisFrame();
            if (keyIndex >= 0)
            {
                SetSelectedIndex(keyIndex, false);
                _suppressHoverUntilPointerMove = true;
                if (!RuntimeInput.TryReadPointerScreenPosition(out _pointerAtKeySelect))
                {
                    _pointerAtKeySelect = Vector2.negativeInfinity;
                }
            }

            TryUpdateHoverFromPointer();

            float dt = Time.unscaledDeltaTime;
            UpdateCardScaleAnimation(dt);
            UpdateContentAnimation(dt);
            UpdateCardThemeAnimation(dt);

            if (RuntimeInput.WasSubmitPressedThisFrame())
            {
                ConfirmSelection(_selectedIndex >= 0 ? _selectedIndex : 0);
            }
        }

    }
}
