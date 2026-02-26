using System;
using System.Text;
using DeliveryRun;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiRunHudManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private const float UiRefreshInterval = 0.1f;
        private const float MinimapRefreshInterval = 0.05f;

        private const string DefaultNowPlayingLabel = "NOW PLAYING: -";
        private const string DefaultCashLabel = "RUN CASH: $0 (+$0)";
        private const string DefaultBuffLabel = "BUFF: -";
        private const string DefaultSynergyLabel = "SYNERGY: -";

        private const int ChoiceSlots = 3;
        private const int MaxTrackedOrders = 8;

        private const float PhoneWidth = 360f;
        private const float PhoneBaseHeight = 86f;
        private const float PhoneRowHeight = 26f;
        private const float PhonePreviewHeight = 180f;
        private const float PhoneSlideSpeed = 720f;

        private const float MinimapCameraHeight = 80f;
        private const float MinimapOrthographicSize = 70f;
        private const int MinimapTextureSize = 512;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private readonly string[] _pickedGenreByChoice = new string[ChoiceSlots];
        private readonly string[] _activeOrderIds = new string[MaxTrackedOrders];
        private readonly string[] _activeOrderTexts = new string[MaxTrackedOrders];

        private UiPrefabCatalogSO _catalog;
        private AddressablesService _addressables;
        private AudioManager _audioManager;
        private MusicLibraryService _musicLibrary;

        private GameObject _hudInstance;
        private RunHudView _view;
        private bool _hudLoadRequested;
        private bool _isRunScene;

        private float _scenePollElapsed;
        private float _uiRefreshElapsed;
        private float _minimapRefreshElapsed;
        private int _lastWholeSecond;

        private string _nowPlayingLabel;
        private string _buffLine;
        private string _synergyLine;
        private float _speedMultiplier;

        private MotorbikeController _player;

        private bool _objectiveMarkerActive;
        private Vector3 _objectiveMarkerWorld;

        private Camera _minimapCamera;
        private RenderTexture _minimapRt;
        private Sprite _circleMaskSprite;
        private RectTransform _minimapMaskRect;
        private RawImage _minimapRawImage;
        private RectTransform _objectiveDotRect;
        private RectTransform _objectiveEdgeRect;
        private Image _objectiveDot;
        private Text _objectiveEdgeText;

        private RectTransform _phonePanelRect;
        private Text _phoneHeaderText;
        private Text _phoneActiveListText;
        private Text _phonePreviewText;
        private float _phoneCurrentHeight;
        private bool _phoneDirty;

        private string _currentOfferId;
        private string _currentPickupName;
        private string _currentDeliveryName;
        private int _currentOfferReward;
        private float _currentOfferRemaining;
        private bool _offerAcceptWindow;
        private bool _previewVisible;

        private int _activeOrderCount;
        private int _sessionBalance;
        private int _sessionBonus;

        private RectTransform _pauseRootRect;
        private GameObject _pausePanel;
        private bool _pauseOpen;
        private bool _pauseCaptured;
        private float _pauseSavedScale;

        public override string Name => nameof(UiRunHudManager);
        public override int InitOrder => 36;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            Services.TryGet(out _addressables);
            Services.TryGet(out _audioManager);
            Services.TryGet(out _musicLibrary);

            ResetState();

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);

            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
            Subs.Add<PlayerMoveSpeedMultiplierChanged>(Events, OnSpeedMultiplierChanged);
            Subs.Add<RunModifiersCleared>(Events, OnModifiersCleared);

            Subs.Add<OfferSpawned>(Events, OnOfferSpawned);
            Subs.Add<OfferTicked>(Events, OnOfferTicked);
            Subs.Add<OfferExpired>(Events, OnOfferExpired);
            Subs.Add<OfferAccepted>(Events, OnOfferAccepted);
            Subs.Add<OrderPickupReached>(Events, OnOrderPickupReached);
            Subs.Add<OrderCompleted>(Events, OnOrderCompleted);
            Subs.Add<OrderObjectiveUpdated>(Events, OnOrderObjectiveUpdated);
            Subs.Add<OrderObjectiveMarkerUpdated>(Events, OnOrderObjectiveMarkerUpdated);
            Subs.Add<SessionBalanceChanged>(Events, OnSessionBalanceChanged);

            HandleSceneChanged(SceneManager.GetActiveScene().name);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollElapsed += unscaledDeltaTime;
            if (_scenePollElapsed >= ScenePollInterval)
            {
                _scenePollElapsed = 0f;
                HandleSceneChanged(SceneManager.GetActiveScene().name);
            }

            if (!_isRunScene)
            {
                return;
            }

            EnsureHudExists();
            if (_view == null)
            {
                return;
            }

            HandleOfferAcceptInput();
            UpdatePhonePanel(unscaledDeltaTime);

            _uiRefreshElapsed += unscaledDeltaTime;
            if (_uiRefreshElapsed >= UiRefreshInterval)
            {
                _uiRefreshElapsed = 0f;
                RefreshTimeLabel();
                RefreshStatusHud();
                ApplyCashLabel();
                RebuildPhoneTextIfNeeded();
            }

            _minimapRefreshElapsed += unscaledDeltaTime;
            if (_minimapRefreshElapsed >= MinimapRefreshInterval)
            {
                _minimapRefreshElapsed = 0f;
                RefreshMinimap();
            }
        }

        protected override void OnShutdown()
        {
            ClosePause(true);
            DestroyHud();
            CleanupMinimap();
            DestroyCircleMask();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            _offerAcceptWindow = false;
            _previewVisible = false;
            _objectiveMarkerActive = false;
            ClosePause(true);
            DestroyHud();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }

        private void OnRunSessionStarted(DomainRunSessionStarted evt)
        {
            _sessionBalance = 0;
            _sessionBonus = 0;
            _speedMultiplier = 1f;
            _buffLine = DefaultBuffLabel;
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
            }
            _synergyLine = DefaultSynergyLabel;

            _offerAcceptWindow = false;
            _previewVisible = false;
            _activeOrderCount = 0;
            _phoneDirty = true;

            if (_view != null)
            {
                _view.SetNowPlaying(_nowPlayingLabel);
                ApplyStatusLines();
                ApplyCashLabel();
                RebuildPhoneTextIfNeeded();
            }
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _nowPlayingLabel = "NOW PLAYING: " + ResolveTrackName(evt);
            if (_view != null)
            {
                _view.SetNowPlaying(_nowPlayingLabel);
            }

            if (evt.ChoiceIndex >= 0 && evt.ChoiceIndex < ChoiceSlots)
            {
                _pickedGenreByChoice[evt.ChoiceIndex] = evt.GenreId;
                RebuildSynergyLine();
                ApplyStatusLines();
            }
        }

        private void OnSpeedMultiplierChanged(PlayerMoveSpeedMultiplierChanged evt)
        {
            _speedMultiplier = evt.Multiplier;
            RebuildBuffLine();
            ApplyStatusLines();
        }

        private void OnModifiersCleared(RunModifiersCleared evt)
        {
            _speedMultiplier = 1f;
            RebuildBuffLine();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
            }
            _synergyLine = DefaultSynergyLabel;
            ApplyStatusLines();
        }

        private void OnOfferSpawned(OfferSpawned evt)
        {
            _currentOfferId = string.IsNullOrEmpty(evt.OfferId) ? "A1" : evt.OfferId;
            _currentPickupName = evt.PickupName;
            _currentDeliveryName = evt.DeliveryName;
            _currentOfferReward = evt.Reward;
            _currentOfferRemaining = evt.TtlSeconds;
            _offerAcceptWindow = true;
            _previewVisible = true;
            _phoneDirty = true;
        }

        private void OnOfferTicked(OfferTicked evt)
        {
            if (!string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                return;
            }

            _currentOfferRemaining = evt.RemainingSeconds;
            if (_previewVisible)
            {
                _phoneDirty = true;
            }
        }

        private void OnOfferExpired(OfferExpired evt)
        {
            if (!string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                return;
            }

            _offerAcceptWindow = false;
            _previewVisible = false;
            RemoveActiveOrder(evt.OfferId);
            _phoneDirty = true;
        }
        private void OnOfferAccepted(OfferAccepted evt)
        {
            if (!string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                return;
            }

            _offerAcceptWindow = false;
            _previewVisible = false;
            UpsertActiveOrder(evt.OfferId, string.IsNullOrEmpty(_currentPickupName) ? "GO PICKUP" : "GO PICKUP: " + _currentPickupName);
            _phoneDirty = true;
        }

        private void OnOrderPickupReached(OrderPickupReached evt)
        {
            if (!string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                return;
            }

            UpsertActiveOrder(evt.OfferId, string.IsNullOrEmpty(_currentDeliveryName) ? "DELIVER TO" : "DELIVER TO: " + _currentDeliveryName);
            _phoneDirty = true;
        }

        private void OnOrderCompleted(OrderCompleted evt)
        {
            if (string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                _offerAcceptWindow = false;
                _previewVisible = false;
            }

            int baseReward = _currentOfferReward > 0 ? _currentOfferReward : evt.Reward;
            _sessionBonus += evt.Reward - baseReward;
            RemoveActiveOrder(evt.OfferId);
            _phoneDirty = true;
            ApplyCashLabel();
        }

        private void OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            if (string.IsNullOrEmpty(evt.Text) || _activeOrderCount <= 0)
            {
                return;
            }

            UpsertActiveOrder(_currentOfferId, evt.Text);
            _phoneDirty = true;
        }

        private void OnOrderObjectiveMarkerUpdated(OrderObjectiveMarkerUpdated evt)
        {
            _objectiveMarkerActive = evt.Active;
            _objectiveMarkerWorld = evt.WorldPosition;
        }

        private void OnSessionBalanceChanged(SessionBalanceChanged evt)
        {
            _sessionBalance = evt.Balance;
            ApplyCashLabel();
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool shouldRun = sceneName == SceneNames.RunScene;
            if (_isRunScene == shouldRun)
            {
                if (_isRunScene)
                {
                    EnsureHudExists();
                }

                return;
            }

            _isRunScene = shouldRun;
            if (!_isRunScene)
            {
                _offerAcceptWindow = false;
                _previewVisible = false;
                _objectiveMarkerActive = false;
                ClosePause(true);
                DestroyHud();
                return;
            }

            _lastWholeSecond = int.MinValue;
            _uiRefreshElapsed = UiRefreshInterval;
            _minimapRefreshElapsed = MinimapRefreshInterval;
            EnsureHudExists();
        }

        private void EnsureHudExists()
        {
            if (_view != null)
            {
                return;
            }

            if (_catalog == null)
            {
                _catalog = UiPrefabCatalogLoader.LoadOrNull();
                if (_catalog == null)
                {
                    return;
                }
            }

            if (_addressables == null)
            {
                Services.TryGet(out _addressables);
            }

            if (_addressables != null && _addressables.IsAvailable && !string.IsNullOrEmpty(_catalog.RunHudKey))
            {
                if (_hudLoadRequested)
                {
                    return;
                }

                _hudLoadRequested = true;
                _addressables.InstantiatePrefab(_catalog.RunHudKey, null, OnHudInstantiated);
                return;
            }

            if (_catalog.RunHudPrefab == null)
            {
                Debug.LogError("[UiRunHudManager] RunHUD key/prefab is not available.");
                return;
            }

            _hudInstance = Object.Instantiate(_catalog.RunHudPrefab);
            BindHud(_hudInstance);
        }

        private void OnHudInstantiated(GameObject hudObject)
        {
            _hudLoadRequested = false;
            if (hudObject == null)
            {
                return;
            }

            if (!_isRunScene)
            {
                ReleaseHud(hudObject);
                return;
            }

            _hudInstance = hudObject;
            BindHud(hudObject);
        }

        private void BindHud(GameObject hudObject)
        {
            _view = hudObject != null ? hudObject.GetComponent<RunHudView>() : null;
            if (_view == null)
            {
                Debug.LogError("[UiRunHudManager] RunHUD prefab missing RunHudView.");
                ReleaseHud(hudObject);
                _hudInstance = null;
                return;
            }

            _view.ConfigureStatusHudCompact();
            _view.SetDeliveryPanelVisible(false);
            _view.SetFocusButtonAction(null);
            _view.SetNowPlaying(_nowPlayingLabel);

            RebuildBuffLine();
            RebuildSynergyLine();
            ApplyStatusLines();
            ApplyCashLabel();
            RefreshTimeLabel();

            BuildMinimapUi();
            BuildPhoneUi();
            BuildPauseUi();
            RebuildPhoneTextIfNeeded(true);
        }

        private void DestroyHud()
        {
            ClosePause(true);

            _pauseRootRect = null;
            _pausePanel = null;
            _phonePanelRect = null;
            _phoneHeaderText = null;
            _phoneActiveListText = null;
            _phonePreviewText = null;

            _minimapMaskRect = null;
            _minimapRawImage = null;
            _objectiveDotRect = null;
            _objectiveEdgeRect = null;
            _objectiveDot = null;
            _objectiveEdgeText = null;

            if (_hudInstance != null)
            {
                ReleaseHud(_hudInstance);
            }

            _view = null;
            _hudInstance = null;
            _hudLoadRequested = false;
            CleanupMinimap();
        }

        private void ReleaseHud(GameObject hudObject)
        {
            if (hudObject == null)
            {
                return;
            }

            if (_addressables == null)
            {
                Services.TryGet(out _addressables);
            }

            if (_addressables != null)
            {
                _addressables.ReleaseInstance(hudObject);
                return;
            }

            Object.Destroy(hudObject);
        }

        private void HandleOfferAcceptInput()
        {
            if (!_offerAcceptWindow)
            {
                return;
            }

            if (!RuntimeInput.WasOfferAcceptPressedThisFrame())
            {
                return;
            }

            _offerAcceptWindow = false;
            TryPlayUiClick();
            Events.Publish(new AcceptOfferRequested { OfferId = _currentOfferId });
        }

        private void RefreshTimeLabel()
        {
            if (_view == null)
            {
                return;
            }

            RunSessionManager runSessionManager;
            if (!Services.TryGet(out runSessionManager) || runSessionManager == null)
            {
                _view.SetTimeLabel("Time: --:--");
                return;
            }

            int wholeSeconds = Mathf.CeilToInt(runSessionManager.RemainingSeconds);
            if (wholeSeconds < 0)
            {
                wholeSeconds = 0;
            }

            if (wholeSeconds == _lastWholeSecond)
            {
                return;
            }

            _lastWholeSecond = wholeSeconds;
            int minutes = wholeSeconds / 60;
            int seconds = wholeSeconds - minutes * 60;
            _view.SetTimeLabel("Time: " + minutes.ToString("00") + ":" + seconds.ToString("00"));
        }

        private void RefreshStatusHud()
        {
            if (_view == null)
            {
                return;
            }

            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            int speedKmh = 0;
            if (_player != null)
            {
                speedKmh = Mathf.RoundToInt(Mathf.Max(0f, _player.CurrentSpeed) * 3.6f);
            }

            _view.SetStatusSpeed("SPEED: " + speedKmh + " km/h");
            ApplyStatusLines();
        }

        private void ApplyStatusLines()
        {
            if (_view != null)
            {
                _view.SetStatusBuffAndSynergy(_buffLine, _synergyLine);
            }
        }

        private void RebuildBuffLine()
        {
            float pct = (_speedMultiplier - 1f) * 100f;
            _buffLine = Mathf.Abs(pct) < 0.01f ? DefaultBuffLabel : "BUFF: SPD " + pct.ToString("+0;-0") + "%";
        }

        private string ResolveTrackName(MusicChoiceSelected evt)
        {
            if (_musicLibrary == null)
            {
                Services.TryGet(out _musicLibrary);
            }

            if (_musicLibrary != null && !string.IsNullOrEmpty(evt.TrackId))
            {
                MusicTrackSO track;
                if (_musicLibrary.TryGetTrack(evt.TrackId, out track) && track != null && !string.IsNullOrEmpty(track.DisplayName))
                {
                    return track.DisplayName;
                }
            }

            if (evt.OptionIndex == 0) return "NITRO BEAT";
            if (evt.OptionIndex == 1) return "CHILL CRUISE";
            if (evt.OptionIndex == 2) return "RISK BASS";
            return "UNKNOWN";
        }
        private void RebuildSynergyLine()
        {
            if (_musicLibrary == null)
            {
                Services.TryGet(out _musicLibrary);
            }

            if (_musicLibrary == null)
            {
                _synergyLine = DefaultSynergyLabel;
                return;
            }

            _builder.Clear();
            int appended = 0;
            string[] unique = new string[ChoiceSlots];
            int[] counts = new int[ChoiceSlots];
            int uniqueCount = 0;

            for (int i = 0; i < ChoiceSlots; i++)
            {
                string id = _pickedGenreByChoice[i];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                int found = -1;
                for (int u = 0; u < uniqueCount; u++)
                {
                    if (string.Equals(unique[u], id, StringComparison.Ordinal))
                    {
                        found = u;
                        break;
                    }
                }

                if (found >= 0)
                {
                    counts[found]++;
                }
                else
                {
                    unique[uniqueCount] = id;
                    counts[uniqueCount] = 1;
                    uniqueCount++;
                }
            }

            for (int i = 0; i < uniqueCount; i++)
            {
                if (counts[i] < 2)
                {
                    continue;
                }

                MusicGenreSO genre;
                if (!_musicLibrary.TryGetGenre(unique[i], out genre) || genre == null)
                {
                    continue;
                }

                MusicSynergySO synergy = _musicLibrary.GetBestSynergyForGenre(genre, counts[i]);
                if (synergy == null)
                {
                    continue;
                }

                if (appended > 0)
                {
                    _builder.Append(" / ");
                }

                _builder.Append(string.IsNullOrEmpty(synergy.DisplayName) ? genre.DisplayName : synergy.DisplayName);
                appended++;
            }

            _synergyLine = appended > 0 ? "SYNERGY: " + _builder : DefaultSynergyLabel;
        }

        private void ApplyCashLabel()
        {
            if (_view == null)
            {
                return;
            }

            if (_sessionBalance == 0 && _sessionBonus == 0)
            {
                _view.SetCash(DefaultCashLabel);
                return;
            }

            string bonus = _sessionBonus >= 0 ? "+$" + _sessionBonus.ToString("N0") : "-$" + Mathf.Abs(_sessionBonus).ToString("N0");
            _view.SetCash("RUN CASH: $" + _sessionBalance.ToString("N0") + " (" + bonus + ")");
        }

        private void UpsertActiveOrder(string offerId, string text)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            int idx = -1;
            for (int i = 0; i < _activeOrderCount; i++)
            {
                if (string.Equals(_activeOrderIds[i], offerId, StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                _activeOrderTexts[idx] = text;
                return;
            }

            if (_activeOrderCount >= MaxTrackedOrders)
            {
                _activeOrderCount = MaxTrackedOrders - 1;
            }

            _activeOrderIds[_activeOrderCount] = offerId;
            _activeOrderTexts[_activeOrderCount] = text;
            _activeOrderCount++;
        }

        private void RemoveActiveOrder(string offerId)
        {
            if (string.IsNullOrEmpty(offerId) || _activeOrderCount <= 0)
            {
                return;
            }

            int idx = -1;
            for (int i = 0; i < _activeOrderCount; i++)
            {
                if (string.Equals(_activeOrderIds[i], offerId, StringComparison.Ordinal))
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                return;
            }

            for (int i = idx; i < _activeOrderCount - 1; i++)
            {
                _activeOrderIds[i] = _activeOrderIds[i + 1];
                _activeOrderTexts[i] = _activeOrderTexts[i + 1];
            }

            _activeOrderCount--;
            _activeOrderIds[_activeOrderCount] = null;
            _activeOrderTexts[_activeOrderCount] = null;
        }

        private void BuildPhoneUi()
        {
            if (_view == null)
            {
                return;
            }

            RectTransform root = _view.GetRootRectTransform();
            if (root == null || _phonePanelRect != null)
            {
                return;
            }

            GameObject panelObject = new GameObject("OrderPhonePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _phonePanelRect = panelObject.GetComponent<RectTransform>();
            _phonePanelRect.SetParent(root, false);
            _phonePanelRect.anchorMin = new Vector2(1f, 0f);
            _phonePanelRect.anchorMax = new Vector2(1f, 0f);
            _phonePanelRect.pivot = new Vector2(1f, 0f);
            _phonePanelRect.anchoredPosition = new Vector2(-28f, 24f);
            _phoneCurrentHeight = PhoneBaseHeight;
            _phonePanelRect.sizeDelta = new Vector2(PhoneWidth, _phoneCurrentHeight);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = _view.GetPanelSkinSprite();
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.11f, 0.13f, 0.16f, 0.93f);
            panelImage.raycastTarget = false;

            _phoneHeaderText = CreateText("Header", _phonePanelRect, 20, TextAnchor.UpperLeft);
            AnchorStretchTop(_phoneHeaderText.rectTransform, 16f, 16f, 12f, 30f);

            _phoneActiveListText = CreateText("ActiveList", _phonePanelRect, 16, TextAnchor.UpperLeft);
            AnchorStretchTop(_phoneActiveListText.rectTransform, 16f, 16f, 42f, 80f);
            _phoneActiveListText.verticalOverflow = VerticalWrapMode.Overflow;

            _phonePreviewText = CreateText("Preview", _phonePanelRect, 17, TextAnchor.MiddleCenter);
            AnchorStretch(_phonePreviewText.rectTransform, 14f, 14f, 10f, 18f);

            _phoneDirty = true;
        }

        private void UpdatePhonePanel(float dt)
        {
            if (_phonePanelRect == null)
            {
                return;
            }

            float target = PhoneBaseHeight + (_activeOrderCount * PhoneRowHeight) + (_previewVisible ? PhonePreviewHeight : 0f);
            _phoneCurrentHeight = Mathf.MoveTowards(_phoneCurrentHeight, target, PhoneSlideSpeed * dt);
            _phonePanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _phoneCurrentHeight);
        }

        private void RebuildPhoneTextIfNeeded(bool force = false)
        {
            if (!force && !_phoneDirty)
            {
                return;
            }

            if (_phoneHeaderText == null || _phoneActiveListText == null || _phonePreviewText == null)
            {
                return;
            }

            _phoneDirty = false;
            _phoneHeaderText.text = _activeOrderCount > 0 ? "Orders (" + _activeOrderCount + ")" : "Orders";

            if (_activeOrderCount <= 0)
            {
                _phoneActiveListText.text = "No active orders";
            }
            else
            {
                _builder.Clear();
                for (int i = 0; i < _activeOrderCount; i++)
                {
                    if (i > 0) _builder.Append('\n');
                    _builder.Append("- ").Append(_activeOrderTexts[i]);
                }
                _phoneActiveListText.text = _builder.ToString();
            }

            if (_previewVisible)
            {
                _builder.Clear();
                _builder.Append("NEW ORDER");
                if (!string.IsNullOrEmpty(_currentPickupName)) _builder.Append('\n').Append(_currentPickupName);
                if (!string.IsNullOrEmpty(_currentDeliveryName)) _builder.Append('\n').Append("-> ").Append(_currentDeliveryName);
                _builder.Append('\n').Append("Reward $").Append(_currentOfferReward);
                _builder.Append('\n').Append("TTL ").Append(_currentOfferRemaining.ToString("0.0")).Append("s");
                _builder.Append('\n').Append("[SPACE] Accept");
                _phonePreviewText.text = _builder.ToString();
                _phonePreviewText.gameObject.SetActive(true);
            }
            else
            {
                _phonePreviewText.gameObject.SetActive(false);
            }
        }

        private void BuildPauseUi()
        {
            if (_view == null)
            {
                return;
            }

            RectTransform root = _view.GetRootRectTransform();
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
            _pauseRootRect.anchoredPosition = new Vector2(-24f, -24f);
            _pauseRootRect.sizeDelta = new Vector2(260f, 220f);

            Button pauseButton = CreateButton("PauseButton", _pauseRootRect, "Pause");
            RectTransform pauseButtonRect = pauseButton.transform as RectTransform;
            pauseButtonRect.anchorMin = new Vector2(1f, 1f);
            pauseButtonRect.anchorMax = new Vector2(1f, 1f);
            pauseButtonRect.pivot = new Vector2(1f, 1f);
            pauseButtonRect.anchoredPosition = new Vector2(-10f, -10f);
            pauseButtonRect.sizeDelta = new Vector2(120f, 36f);
            pauseButton.onClick.AddListener(TogglePause);

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
            panelImage.sprite = _view.GetPanelSkinSprite();
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            Button resumeButton = CreateButton("ResumeButton", panelRect, "Resume");
            RectTransform resumeRect = resumeButton.transform as RectTransform;
            resumeRect.anchorMin = new Vector2(0.5f, 1f);
            resumeRect.anchorMax = new Vector2(0.5f, 1f);
            resumeRect.pivot = new Vector2(0.5f, 1f);
            resumeRect.anchoredPosition = new Vector2(0f, -42f);
            resumeRect.sizeDelta = new Vector2(140f, 34f);
            resumeButton.onClick.AddListener(() => ClosePause(true));

            Button lobbyButton = CreateButton("LobbyButton", panelRect, "Back Lobby");
            RectTransform lobbyRect = lobbyButton.transform as RectTransform;
            lobbyRect.anchorMin = new Vector2(0.5f, 1f);
            lobbyRect.anchorMax = new Vector2(0.5f, 1f);
            lobbyRect.pivot = new Vector2(0.5f, 1f);
            lobbyRect.anchoredPosition = new Vector2(0f, -82f);
            lobbyRect.sizeDelta = new Vector2(140f, 34f);
            lobbyButton.onClick.AddListener(() =>
            {
                ClosePause(true);
                Events.Publish(new ReturnToLobbyRequested());
            });

            _pausePanel.SetActive(false);
        }

        private void TogglePause()
        {
            if (_pauseOpen)
            {
                ClosePause(true);
                return;
            }

            OpenPause();
        }

        private void OpenPause()
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

        private void ClosePause(bool restore)
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
        private void BuildMinimapUi()
        {
            if (_view == null)
            {
                return;
            }

            RectTransform panelRect = _view.GetMinimapPanelRectTransform();
            Image maskImage = _view.GetMinimapImage();
            if (panelRect == null || maskImage == null)
            {
                return;
            }

            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(24f, 24f);
            panelRect.sizeDelta = new Vector2(220f, 220f);

            Image panelImage = panelRect.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.08f, 0.1f, 0.13f, 0.88f);
                panelImage.raycastTarget = false;
            }

            if (_circleMaskSprite == null)
            {
                _circleMaskSprite = CreateCircleMaskSprite(256);
            }

            Mask mask = maskImage.GetComponent<Mask>();
            if (mask == null)
            {
                mask = maskImage.gameObject.AddComponent<Mask>();
            }
            mask.showMaskGraphic = true;
            maskImage.sprite = _circleMaskSprite;
            maskImage.type = Image.Type.Simple;
            maskImage.color = new Color(0.03f, 0.03f, 0.03f, 0.98f);
            maskImage.raycastTarget = false;

            _minimapMaskRect = maskImage.rectTransform;
            _minimapMaskRect.anchorMin = new Vector2(0f, 0f);
            _minimapMaskRect.anchorMax = new Vector2(1f, 1f);
            _minimapMaskRect.offsetMin = new Vector2(8f, 8f);
            _minimapMaskRect.offsetMax = new Vector2(-8f, -8f);

            Transform mapRender = _minimapMaskRect.Find("MapRender");
            if (mapRender == null)
            {
                GameObject go = new GameObject("MapRender", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(_minimapMaskRect, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                _minimapRawImage = go.GetComponent<RawImage>();
            }
            else
            {
                _minimapRawImage = mapRender.GetComponent<RawImage>();
                if (_minimapRawImage == null)
                {
                    _minimapRawImage = mapRender.gameObject.AddComponent<RawImage>();
                }
            }
            _minimapRawImage.raycastTarget = false;

            EnsureMinimapCamera();
            _minimapRawImage.texture = _minimapRt;
            _minimapRawImage.color = Color.white;

            RectTransform overlay = _minimapMaskRect.Find("Overlay") as RectTransform;
            if (overlay == null)
            {
                GameObject overlayGo = new GameObject("Overlay", typeof(RectTransform));
                overlay = overlayGo.GetComponent<RectTransform>();
                overlay.SetParent(_minimapMaskRect, false);
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = Vector2.zero;
                overlay.offsetMax = Vector2.zero;
            }

            _objectiveDot = EnsureMarkerImage(overlay, "ObjectiveDot", new Color(1f, 0.84f, 0.2f, 1f), 14f);
            _objectiveDotRect = _objectiveDot.rectTransform;
            _objectiveDot.gameObject.SetActive(false);

            _objectiveEdgeText = EnsureMarkerText(overlay, "ObjectiveEdge", "бу", new Color(1f, 0.9f, 0.2f, 1f), 26);
            _objectiveEdgeRect = _objectiveEdgeText.rectTransform;
            _objectiveEdgeText.gameObject.SetActive(false);

            Text playerArrow = EnsureMarkerText(overlay, "PlayerArrow", "бу", new Color(0.25f, 1f, 0.8f, 1f), 24);
            playerArrow.rectTransform.anchoredPosition = Vector2.zero;
            playerArrow.rectTransform.localRotation = Quaternion.identity;
        }

        private void EnsureMinimapCamera()
        {
            if (_minimapCamera == null)
            {
                GameObject cameraObject = GameObject.Find("RunMiniMapCamera");
                if (cameraObject == null)
                {
                    cameraObject = new GameObject("RunMiniMapCamera");
                }

                _minimapCamera = cameraObject.GetComponent<Camera>();
                if (_minimapCamera == null)
                {
                    _minimapCamera = cameraObject.AddComponent<Camera>();
                }
            }

            _minimapCamera.orthographic = true;
            _minimapCamera.orthographicSize = MinimapOrthographicSize;
            _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            _minimapCamera.backgroundColor = new Color(0.18f, 0.2f, 0.22f, 1f);
            _minimapCamera.cullingMask = ~0;
            _minimapCamera.nearClipPlane = 0.01f;
            _minimapCamera.farClipPlane = 400f;
            _minimapCamera.depth = -80f;
            _minimapCamera.allowHDR = false;
            _minimapCamera.allowMSAA = false;

            if (_minimapRt == null || _minimapRt.width != MinimapTextureSize || _minimapRt.height != MinimapTextureSize)
            {
                if (_minimapRt != null)
                {
                    _minimapRt.Release();
                    Object.Destroy(_minimapRt);
                }

                _minimapRt = new RenderTexture(MinimapTextureSize, MinimapTextureSize, 16, RenderTextureFormat.ARGB32);
                _minimapRt.name = "RunMinimapRT";
                _minimapRt.useMipMap = false;
                _minimapRt.autoGenerateMips = false;
                _minimapRt.Create();
            }

            _minimapCamera.targetTexture = _minimapRt;
        }

        private void RefreshMinimap()
        {
            if (_minimapCamera == null || _minimapMaskRect == null)
            {
                return;
            }

            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_player == null)
            {
                if (_objectiveDot != null) _objectiveDot.gameObject.SetActive(false);
                if (_objectiveEdgeText != null) _objectiveEdgeText.gameObject.SetActive(false);
                return;
            }

            Transform p = _player.transform;
            float yaw = p.eulerAngles.y;
            _minimapCamera.transform.SetPositionAndRotation(p.position + Vector3.up * MinimapCameraHeight, Quaternion.Euler(90f, yaw, 0f));

            if (_objectiveDot == null || _objectiveEdgeText == null)
            {
                return;
            }

            if (!_objectiveMarkerActive)
            {
                _objectiveDot.gameObject.SetActive(false);
                _objectiveEdgeText.gameObject.SetActive(false);
                return;
            }

            Vector3 delta = _objectiveMarkerWorld - p.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f)
            {
                _objectiveDot.gameObject.SetActive(false);
                _objectiveEdgeText.gameObject.SetActive(false);
                return;
            }

            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * delta;
            Vector2 flat = new Vector2(local.x, local.z);
            float uiRadius = Mathf.Min(_minimapMaskRect.rect.width, _minimapMaskRect.rect.height) * 0.5f - 10f;
            Vector2 uiPos = flat * (uiRadius / Mathf.Max(1f, _minimapCamera.orthographicSize));

            if (uiPos.magnitude <= uiRadius * 0.86f)
            {
                _objectiveDot.gameObject.SetActive(true);
                _objectiveEdgeText.gameObject.SetActive(false);
                _objectiveDotRect.anchoredPosition = uiPos;
                return;
            }

            Vector2 dir = uiPos.normalized;
            _objectiveDot.gameObject.SetActive(false);
            _objectiveEdgeText.gameObject.SetActive(true);
            _objectiveEdgeRect.anchoredPosition = dir * (uiRadius * 0.9f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _objectiveEdgeRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        private void CleanupMinimap()
        {
            if (_minimapCamera != null)
            {
                _minimapCamera.targetTexture = null;
                Object.Destroy(_minimapCamera.gameObject);
                _minimapCamera = null;
            }

            if (_minimapRt != null)
            {
                _minimapRt.Release();
                Object.Destroy(_minimapRt);
                _minimapRt = null;
            }
        }

        private void DestroyCircleMask()
        {
            if (_circleMaskSprite == null)
            {
                return;
            }

            Texture2D texture = _circleMaskSprite.texture;
            Object.Destroy(_circleMaskSprite);
            _circleMaskSprite = null;
            if (texture != null)
            {
                Object.Destroy(texture);
            }
        }

        private void TryPlayUiClick()
        {
            if (_audioManager == null)
            {
                Services.TryGet(out _audioManager);
            }

            if (_audioManager == null || _catalog == null)
            {
                return;
            }

            _audioManager.PlayUiClick(_catalog.UiClickKey);
        }

        private void ResetState()
        {
            _nowPlayingLabel = DefaultNowPlayingLabel;
            _buffLine = DefaultBuffLabel;
            _synergyLine = DefaultSynergyLabel;
            _speedMultiplier = 1f;
            _offerAcceptWindow = false;
            _previewVisible = false;
            _currentOfferId = "A1";
            _currentPickupName = string.Empty;
            _currentDeliveryName = string.Empty;
            _currentOfferReward = 0;
            _currentOfferRemaining = 0f;
            _activeOrderCount = 0;
            _sessionBalance = 0;
            _sessionBonus = 0;
            _phoneDirty = true;
            _lastWholeSecond = int.MinValue;
            _objectiveMarkerActive = false;
            _objectiveMarkerWorld = Vector3.zero;
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = _view != null ? _view.GetDefaultFont() : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            t.raycastTarget = false;
            return t;
        }

        private Button CreateButton(string name, Transform parent, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            Image img = go.GetComponent<Image>();
            img.sprite = _view != null ? _view.GetPanelSkinSprite() : null;
            img.type = img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = new Color(0.16f, 0.2f, 0.28f, 0.95f);

            Button btn = go.GetComponent<Button>();
            Text labelText = CreateText("Label", rt, 16, TextAnchor.MiddleCenter);
            AnchorStretch(labelText.rectTransform, 8f, 8f, 4f, 4f);
            labelText.text = label;
            return btn;
        }

        private static void AnchorStretchTop(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void AnchorStretch(RectTransform rt, float left, float right, float bottom, float top)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static Image EnsureMarkerImage(RectTransform parent, string name, Color color, float size)
        {
            Transform tr = parent.Find(name);
            Image img;
            if (tr == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.sizeDelta = new Vector2(size, size);
                img = go.GetComponent<Image>();
            }
            else
            {
                img = tr.GetComponent<Image>() ?? tr.gameObject.AddComponent<Image>();
            }

            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text EnsureMarkerText(RectTransform parent, string name, string value, Color color, int fontSize)
        {
            Transform tr = parent.Find(name);
            Text text;
            if (tr == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.sizeDelta = new Vector2(28f, 28f);
                text = go.GetComponent<Text>();
            }
            else
            {
                text = tr.GetComponent<Text>() ?? tr.gameObject.AddComponent<Text>();
            }

            text.font = _view != null ? _view.GetDefaultFont() : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite CreateCircleMaskSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "MinimapCircleMask";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            float radiusSq = radius * radius;
            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 fill = new Color32(255, 255, 255, 255);

            Color32[] pixels = new Color32[size * size];
            int idx = 0;
            for (int y = 0; y < size; y++)
            {
                float dy = y - center;
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    pixels[idx++] = (dx * dx + dy * dy) <= radiusSq ? fill : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
