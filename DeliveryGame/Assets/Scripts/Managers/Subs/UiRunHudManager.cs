using System;
using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiRunHudManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private const float UiRefreshInterval = 0.1f;
        private const float MinimapRefreshInterval = 0.05f;

        private const int ChoiceSlots = 3;
        private const int MaxTrackedOrders = 8;

        private readonly string[] _pickedGenreByChoice = new string[ChoiceSlots];
        private readonly string[] _pickedTrackNameByChoice = new string[ChoiceSlots];
        private readonly string[] _activeOrderIds = new string[MaxTrackedOrders];
        private readonly string[] _activeOrderTexts = new string[MaxTrackedOrders];
        private readonly Dictionary<string, string> _offerPickupNames = new Dictionary<string, string>(MaxTrackedOrders);
        private readonly Dictionary<string, string> _offerDeliveryNames = new Dictionary<string, string>(MaxTrackedOrders);
        private readonly Dictionary<string, int> _offerBaseRewards = new Dictionary<string, int>(MaxTrackedOrders);
        private readonly Dictionary<string, bool> _orderCarryingByOffer = new Dictionary<string, bool>(MaxTrackedOrders);

        private UiPrefabCatalogSO _catalog;
        private AddressablesService _addressables;
        private AudioManager _audioManager;
        private MusicLibraryService _musicLibrary;
        private ModifierStackService _modifierStack;
        private OrderFlowManager _orderFlowManager;

        private GameObject _hudInstance;
        private RunHudView _view;
        private bool _hudLoadRequested;
        private bool _isRunScene;
        private bool _musicChoiceModalOpen;

        private float _scenePollElapsed;
        private float _uiRefreshElapsed;
        private float _minimapRefreshElapsed;
        private int _lastWholeSecond;

        private float _speedMultiplier;
        private float _currentSpeedKmh;
        private float _foodTemperature01;
        private float _foodSpill01;
        private float _foodQuality01;
        private bool _hasFoodState;
        private string _foodOfferId;

        private MotorbikeController _player;

        private Sprite _circleRingSprite;
        private HudMinimapPanel _minimapPanel;

        private HudPhonePanel _phonePanel;

        private float _fuel01 = 1f;
        private HudGaugePanel _gaugePanel;
        private HudTrackPlayerPanel _trackPlayerPanel;

        private HudWorldOrderTimerPanel _worldOrderTimerPanel;

        private string _currentOfferId;
        private string _currentPickupName;
        private string _currentDeliveryName;
        private int _currentOfferReward;
        private float _currentOfferRemaining;
        private float _currentOfferDuration;
        private bool _offerAcceptWindow;
        private bool _previewVisible;
        private double _offerUiExpireAt;
        private bool _offerUiPauseActive;
        private double _offerUiPauseStartedAt;
        private int _offerUiLastTenth;

        private int _activeOrderCount;
        private int _sessionBalance;
        private int _sessionBonus;

        private HudPausePanel _pausePanelController;
        private HudStatusDisplay _statusDisplay;

        public override string Name => nameof(UiRunHudManager);
        public override int InitOrder => 36;
        public bool IsPauseMenuOpen => _pausePanelController != null && _pausePanelController.IsOpen;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            Services.TryGet(out _addressables);
            Services.TryGet(out _audioManager);
            Services.TryGet(out _musicLibrary);
            Services.TryGet(out _modifierStack);
            Services.TryGet(out _orderFlowManager);

            ResetState();

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<DomainRunSessionStarted>(Events, OnRunSessionStarted);

            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
            Subs.Add<MusicChoiceModalStateChanged>(Events, OnMusicChoiceModalStateChanged);
            Subs.Add<PlayerMoveSpeedMultiplierChanged>(Events, OnSpeedMultiplierChanged);
            Subs.Add<RunModifiersCleared>(Events, OnModifiersCleared);
            Subs.Add<RunModifiersChanged>(Events, OnModifiersChanged);

            Subs.Add<OfferSpawned>(Events, OnOfferSpawned);
            Subs.Add<OfferTicked>(Events, OnOfferTicked);
            Subs.Add<OfferExpired>(Events, OnOfferExpired);
            Subs.Add<OfferAccepted>(Events, OnOfferAccepted);
            Subs.Add<OrderPickupReached>(Events, OnOrderPickupReached);
            Subs.Add<OrderCompleted>(Events, OnOrderCompleted);
            Subs.Add<OrderTimedOut>(Events, OnOrderTimedOut);
            Subs.Add<OrderObjectiveUpdated>(Events, OnOrderObjectiveUpdated);
            Subs.Add<OrderObjectiveMarkerUpdated>(Events, OnOrderObjectiveMarkerUpdated);
            Subs.Add<OrderObjectiveMarkersUpdated>(Events, OnOrderObjectiveMarkersUpdated);
            Subs.Add<SessionBalanceChanged>(Events, OnSessionBalanceChanged);
            Subs.Add<FoodStateTicked>(Events, OnFoodStateTicked);
            Subs.Add<FuelStateChanged>(Events, OnFuelStateChanged);
            Subs.Add<FuelRefuelStateChanged>(Events, OnFuelRefuelStateChanged);

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
            UpdateOfferCountdownFromClock();
            UpdatePhonePanel(unscaledDeltaTime);
            UpdateWorldOrderTimerTransform();

            _uiRefreshElapsed += unscaledDeltaTime;
            if (_uiRefreshElapsed >= UiRefreshInterval)
            {
                _uiRefreshElapsed = 0f;
                RefreshTimeLabel();
                RefreshStatusHud();
                ApplyCashLabel();
                RebuildPhoneTextIfNeeded();
                RefreshWorldOrderTimerUi();
            }

            _minimapRefreshElapsed += unscaledDeltaTime;
            if (_minimapRefreshElapsed >= MinimapRefreshInterval)
            {
                _minimapRefreshElapsed = 0f;
                _minimapPanel?.Refresh(ref _player);
            }
        }

        protected override void OnShutdown()
        {
            ClosePause(true);
            DestroyHud();
            _minimapPanel?.Cleanup();
            DestroyCircleRing();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (SceneNames.IsRunSceneLike(evt.To))
            {
                return;
            }

            _isRunScene = false;
            _offerAcceptWindow = false;
            _previewVisible = false;
            _currentOfferRemaining = 0f;
            _currentOfferDuration = 0f;
            _offerUiExpireAt = 0d;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = -1;
            _foodOfferId = null;
            _hasFoodState = false;
            _orderCarryingByOffer.Clear();
            _minimapPanel?.ResetRuntimeState();
            _musicChoiceModalOpen = false;
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
            _currentSpeedKmh = 0f;
            _statusDisplay?.Reset();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
                _pickedTrackNameByChoice[i] = null;
            }
            _hasFoodState = false;
            _foodOfferId = null;
            _foodTemperature01 = 0f;
            _foodSpill01 = 0f;
            _foodQuality01 = 0f;
            _fuel01 = 1f;

            _offerAcceptWindow = false;
            _previewVisible = false;
            _currentOfferRemaining = 0f;
            _currentOfferDuration = 0f;
            _offerUiExpireAt = 0d;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = -1;
            _musicChoiceModalOpen = false;
            _activeOrderCount = 0;
            _offerPickupNames.Clear();
            _offerDeliveryNames.Clear();
            _offerBaseRewards.Clear();
            _orderCarryingByOffer.Clear();
            _phonePanel?.ResetRuntimeState();
            MarkPhoneDirty();
            _minimapPanel?.ResetRuntimeState();

            if (_view != null)
            {
                ApplyStatusLines();
                ApplyCashLabel();
                RefreshTrackPlayerText();
                UpdateSpeedGaugeVisual();
                UpdateFuelGaugeVisual();
                RebuildPhoneTextIfNeeded();
            }
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            string trackName = ResolveTrackName(evt);
            if (evt.ChoiceIndex >= 0 && evt.ChoiceIndex < ChoiceSlots)
            {
                _pickedGenreByChoice[evt.ChoiceIndex] = evt.GenreId;
                _pickedTrackNameByChoice[evt.ChoiceIndex] = trackName;
                RebuildSynergyLine();
            }

            RefreshTrackPlayerText();
            ApplyStatusLines();
        }

        private void OnMusicChoiceModalStateChanged(MusicChoiceModalStateChanged evt)
        {
            _musicChoiceModalOpen = evt.IsOpen;

            if (!_offerAcceptWindow || !_previewVisible)
            {
                _offerUiPauseActive = false;
                _offerUiPauseStartedAt = 0d;
                return;
            }

            if (evt.IsOpen)
            {
                if (_offerUiPauseActive)
                {
                    return;
                }

                _offerUiPauseActive = true;
                _offerUiPauseStartedAt = Clock.Now;
                return;
            }

            if (!_offerUiPauseActive)
            {
                return;
            }

            _offerUiPauseActive = false;
            double pausedSeconds = Clock.Now - _offerUiPauseStartedAt;
            _offerUiPauseStartedAt = 0d;
            if (pausedSeconds > 0d && _offerUiExpireAt > 0d)
            {
                _offerUiExpireAt += pausedSeconds;
            }
        }

        private void OnSpeedMultiplierChanged(PlayerMoveSpeedMultiplierChanged evt)
        {
            _speedMultiplier = evt.Multiplier;
            RebuildBuffLineFromModifiers();
            ApplyStatusLines();
        }

        private void OnModifiersChanged(RunModifiersChanged evt)
        {
            RebuildBuffLineFromModifiers();
            ApplyStatusLines();
        }

        private void OnModifiersCleared(RunModifiersCleared evt)
        {
            _speedMultiplier = 1f;
            RebuildBuffLineFromModifiers();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
                _pickedTrackNameByChoice[i] = null;
            }
            RebuildSynergyLine();
            RefreshTrackPlayerText();
            ApplyStatusLines();
        }

        private void OnOfferSpawned(OfferSpawned evt)
        {
            _currentOfferId = string.IsNullOrEmpty(evt.OfferId) ? "A1" : evt.OfferId;
            _currentPickupName = evt.PickupName;
            _currentDeliveryName = evt.DeliveryName;
            _offerPickupNames[_currentOfferId] = _currentPickupName ?? string.Empty;
            _offerDeliveryNames[_currentOfferId] = _currentDeliveryName ?? string.Empty;
            _offerBaseRewards[_currentOfferId] = evt.Reward;
            _currentOfferReward = evt.Reward;
            _currentOfferRemaining = evt.TtlSeconds;
            _currentOfferDuration = Mathf.Max(0.01f, evt.TtlSeconds);
            _offerAcceptWindow = true;
            _previewVisible = true;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = Mathf.FloorToInt(_currentOfferRemaining * 10f);
            _offerUiExpireAt = Clock.Now + evt.TtlSeconds;
            MarkPhoneDirty();
        }

        private void OnOfferTicked(OfferTicked evt)
        {
            if (!string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                return;
            }

            _currentOfferRemaining = evt.RemainingSeconds;
            _offerUiLastTenth = Mathf.FloorToInt(_currentOfferRemaining * 10f);
            if (_previewVisible)
            {
                MarkPhoneDirty();
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
            _currentOfferRemaining = 0f;
            _offerUiExpireAt = 0d;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = -1;
            RemoveActiveOrder(evt.OfferId);
            _offerPickupNames.Remove(evt.OfferId);
            _offerDeliveryNames.Remove(evt.OfferId);
            _offerBaseRewards.Remove(evt.OfferId);
            _orderCarryingByOffer.Remove(evt.OfferId);
            MarkPhoneDirty();
        }
        private void OnOfferAccepted(OfferAccepted evt)
        {
            if (string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                _offerAcceptWindow = false;
                _previewVisible = false;
                _offerUiExpireAt = 0d;
                _offerUiPauseActive = false;
                _offerUiPauseStartedAt = 0d;
                _offerUiLastTenth = -1;
            }

            string pickupName;
            if (!_offerPickupNames.TryGetValue(evt.OfferId, out pickupName))
            {
                pickupName = _currentPickupName;
            }
            UpsertActiveOrder(evt.OfferId, string.IsNullOrEmpty(pickupName) ? "GO PICKUP" : "GO PICKUP: " + pickupName);
            _orderCarryingByOffer[evt.OfferId] = false;
            MarkPhoneDirty();
        }

        private void OnOrderPickupReached(OrderPickupReached evt)
        {
            string deliveryName;
            if (!_offerDeliveryNames.TryGetValue(evt.OfferId, out deliveryName))
            {
                deliveryName = _currentDeliveryName;
            }
            _foodOfferId = evt.OfferId;
            _orderCarryingByOffer[evt.OfferId] = true;
            UpsertActiveOrder(evt.OfferId, string.IsNullOrEmpty(deliveryName) ? "DELIVER TO" : "DELIVER TO: " + deliveryName);
            MarkPhoneDirty();
        }

        private void OnOrderCompleted(OrderCompleted evt)
        {
            if (string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                _offerAcceptWindow = false;
                _previewVisible = false;
                _offerUiExpireAt = 0d;
                _offerUiPauseActive = false;
                _offerUiPauseStartedAt = 0d;
                _offerUiLastTenth = -1;
            }

            int baseReward;
            if (!_offerBaseRewards.TryGetValue(evt.OfferId, out baseReward) || baseReward <= 0)
            {
                baseReward = _currentOfferReward > 0 ? _currentOfferReward : evt.Reward;
            }
            _sessionBonus += evt.Reward - baseReward;
            RemoveActiveOrder(evt.OfferId);
            _offerPickupNames.Remove(evt.OfferId);
            _offerDeliveryNames.Remove(evt.OfferId);
            _offerBaseRewards.Remove(evt.OfferId);
            _orderCarryingByOffer.Remove(evt.OfferId);
            if (string.Equals(_foodOfferId, evt.OfferId, StringComparison.Ordinal))
            {
                _foodOfferId = null;
                _hasFoodState = false;
                _foodTemperature01 = 0f;
                _foodSpill01 = 0f;
                _foodQuality01 = 0f;
            }
            MarkPhoneDirty();
            ApplyCashLabel();
        }

        private void OnOrderTimedOut(OrderTimedOut evt)
        {
            if (string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                _offerAcceptWindow = false;
                _previewVisible = false;
                _offerUiExpireAt = 0d;
                _offerUiPauseActive = false;
                _offerUiPauseStartedAt = 0d;
                _offerUiLastTenth = -1;
            }

            RemoveActiveOrder(evt.OfferId);
            _offerPickupNames.Remove(evt.OfferId);
            _offerDeliveryNames.Remove(evt.OfferId);
            _offerBaseRewards.Remove(evt.OfferId);
            _orderCarryingByOffer.Remove(evt.OfferId);
            if (string.Equals(_foodOfferId, evt.OfferId, StringComparison.Ordinal))
            {
                _foodOfferId = null;
                _hasFoodState = false;
                _foodTemperature01 = 0f;
                _foodSpill01 = 0f;
                _foodQuality01 = 0f;
            }

            MarkPhoneDirty();
        }

        private void OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            if (string.IsNullOrEmpty(evt.Text) || _activeOrderCount <= 0)
            {
                return;
            }

            string targetOfferId = evt.OfferId;
            if (string.IsNullOrEmpty(targetOfferId))
            {
                targetOfferId = _activeOrderIds[0];
            }
            if (string.IsNullOrEmpty(targetOfferId))
            {
                return;
            }

            UpsertActiveOrder(targetOfferId, evt.Text);
            MarkPhoneDirty();
        }

        private void OnOrderObjectiveMarkerUpdated(OrderObjectiveMarkerUpdated evt)
        {
            _minimapPanel?.SetObjectiveMarker(evt.Active, evt.WorldPosition, evt.PointType);
        }

        private void OnOrderObjectiveMarkersUpdated(OrderObjectiveMarkersUpdated evt)
        {
            _minimapPanel?.SetObjectiveMarkers(evt);
        }

        private void OnSessionBalanceChanged(SessionBalanceChanged evt)
        {
            _sessionBalance = evt.Balance;
            ApplyCashLabel();
        }

        private void OnFoodStateTicked(FoodStateTicked evt)
        {
            _hasFoodState = true;
            _foodTemperature01 = Mathf.Clamp01(evt.Temperature01);
            _foodSpill01 = Mathf.Clamp01(evt.Spill01);
            _foodQuality01 = Mathf.Clamp01(evt.Quality01);
            MarkPhoneDirty();
        }

        private void OnFuelStateChanged(FuelStateChanged evt)
        {
            _fuel01 = Mathf.Clamp01(evt.Fuel01);
            UpdateFuelGaugeVisual();
        }

        private void OnFuelRefuelStateChanged(FuelRefuelStateChanged evt)
        {
            _statusDisplay?.SetRefuelState(evt.IsRefueling, evt.FuelPerSecond, evt.CostPerSecond);
            ApplyStatusLines();
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool shouldRun = SceneNames.IsRunSceneLike(sceneName);
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
                _minimapPanel?.ResetRuntimeState();
                ClosePause(true);
                DestroyHud();
                return;
            }

            _lastWholeSecond = int.MinValue;
            _uiRefreshElapsed = UiRefreshInterval;
            _minimapRefreshElapsed = MinimapRefreshInterval;
            _minimapPanel?.ResetRuntimeState();
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
            _view.SetBottomLeftPanelVisible(false);
            _view.ConfigureCenterStatusMerged();
            _view.SetDeliveryPanelVisible(false);
            _view.SetFocusButtonAction(null);

            RebuildBuffLineFromModifiers();
            RebuildSynergyLine();
            ApplyStatusLines();
            ApplyCashLabel();
            RefreshTimeLabel();

            BuildMinimapUi();
            BuildPhoneUi();
            BuildSpeedGaugeUi();
            BuildFuelGaugeUi();
            BuildTrackPlayerUi();
            BuildPauseUi();
            BuildWorldOrderTimerUi();
            RefreshTrackPlayerText();
            UpdateSpeedGaugeVisual();
            UpdateFuelGaugeVisual();
            RebuildPhoneTextIfNeeded(true);
            RefreshWorldOrderTimerUi();
        }

        private void DestroyHud()
        {
            ClosePause(true);

            _pausePanelController?.Cleanup();
            _pausePanelController = null;
            _gaugePanel?.Cleanup();
            _gaugePanel = null;
            _trackPlayerPanel?.Cleanup();
            _trackPlayerPanel = null;
            _phonePanel?.Cleanup();
            _phonePanel = null;
            _worldOrderTimerPanel?.Cleanup();
            _worldOrderTimerPanel = null;
            _minimapPanel?.Cleanup();

            if (_hudInstance != null)
            {
                ReleaseHud(_hudInstance);
            }

            _view = null;
            _hudInstance = null;
            _hudLoadRequested = false;
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
            if (_musicChoiceModalOpen)
            {
                return;
            }

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

            _currentSpeedKmh = speedKmh;
            UpdateSpeedGaugeVisual();
            RebuildBuffLineFromModifiers();
            ApplyStatusLines();
        }

        private void ApplyStatusLines()
        {
            _statusDisplay?.ApplyStatus(_view);
        }

        private void RebuildBuffLineFromModifiers()
        {
            if (_modifierStack == null)
            {
                Services.TryGet(out _modifierStack);
            }

            _statusDisplay?.RebuildBuffLine(_modifierStack, _speedMultiplier);
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

            _statusDisplay?.RebuildSynergyLine(_musicLibrary, _pickedGenreByChoice);
        }

        private void ApplyCashLabel()
        {
            _statusDisplay?.ApplyCash(_view, _sessionBalance, _sessionBonus);
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
            _orderCarryingByOffer.Remove(offerId);
        }

        private void MarkPhoneDirty()
        {
            _phonePanel?.SetDirty();
        }

        private HudPhonePanel.PhoneViewState BuildPhoneViewState()
        {
            return new HudPhonePanel.PhoneViewState
            {
                ActiveOrderCount = _activeOrderCount,
                HasFoodState = _hasFoodState,
                FoodTemperature01 = _foodTemperature01,
                FoodSpill01 = _foodSpill01,
                FoodOfferId = _foodOfferId,
                PreviewVisible = _previewVisible,
                OfferAcceptWindow = _offerAcceptWindow,
                CurrentOfferDuration = _currentOfferDuration,
                CurrentOfferRemaining = _currentOfferRemaining,
                CurrentPickupName = _currentPickupName,
                CurrentDeliveryName = _currentDeliveryName,
                CurrentOfferReward = _currentOfferReward
            };
        }

        private void UpdateOfferCountdownFromClock()
        {
            if (!_offerAcceptWindow || !_previewVisible || _offerUiPauseActive || _offerUiExpireAt <= 0d)
            {
                return;
            }

            double remainingSeconds = _offerUiExpireAt - Clock.Now;
            if (remainingSeconds < 0d)
            {
                remainingSeconds = 0d;
            }

            float remainingFloat = (float)remainingSeconds;
            int tenth = Mathf.FloorToInt(remainingFloat * 10f);
            if (tenth != _offerUiLastTenth)
            {
                _offerUiLastTenth = tenth;
                MarkPhoneDirty();
            }

            _currentOfferRemaining = remainingFloat;
        }

        private void BuildPhoneUi()
        {
            if (_phonePanel == null)
            {
                _phonePanel = new HudPhonePanel(_activeOrderIds, _activeOrderTexts, _orderCarryingByOffer);
            }

            _phonePanel.BuildIfNeeded(_view);
        }

        private void UpdatePhonePanel(float dt)
        {
            if (_phonePanel == null)
            {
                return;
            }

            _phonePanel.UpdateLayout(dt, BuildPhoneViewState());
        }

        private void RebuildPhoneTextIfNeeded(bool force = false)
        {
            if (_phonePanel == null)
            {
                return;
            }

            _phonePanel.RebuildTextIfNeeded(force, BuildPhoneViewState());
        }

        private void BuildSpeedGaugeUi()
        {
            if (_gaugePanel == null)
            {
                _gaugePanel = new HudGaugePanel();
            }

            if (_circleRingSprite == null)
            {
                _circleRingSprite = HudUiFactory.CreateCircleRingSprite(128, 3f);
            }

            _gaugePanel.BuildIfNeeded(_view, _circleRingSprite);
        }

        private void UpdateSpeedGaugeVisual()
        {
            _gaugePanel?.SetSpeed(_currentSpeedKmh);
        }

        private void BuildFuelGaugeUi()
        {
            if (_gaugePanel == null)
            {
                _gaugePanel = new HudGaugePanel();
            }

            _gaugePanel.BuildIfNeeded(_view, _circleRingSprite);
            _gaugePanel.SetFuel(_fuel01);
        }

        private void UpdateFuelGaugeVisual()
        {
            _gaugePanel?.SetFuel(_fuel01);
        }

        private void BuildTrackPlayerUi()
        {
            if (_trackPlayerPanel == null)
            {
                _trackPlayerPanel = new HudTrackPlayerPanel();
            }

            _trackPlayerPanel.BuildIfNeeded(_view);
        }

        private void RefreshTrackPlayerText()
        {
            _trackPlayerPanel?.Refresh(_pickedTrackNameByChoice);
        }

        private void BuildPauseUi()
        {
            if (_pausePanelController == null)
            {
                _pausePanelController = new HudPausePanel();
            }

            _pausePanelController.BuildIfNeeded(_view, () => Events.Publish(new ReturnToLobbyRequested()), TryPlayUiClick);
        }

        private void TogglePause()
        {
            if (_pausePanelController == null)
            {
                return;
            }

            if (_pausePanelController.IsOpen)
            {
                ClosePause(true);
                return;
            }

            OpenPause();
        }

        private void OpenPause()
        {
            _pausePanelController?.Open();
        }

        private void ClosePause(bool restore)
        {
            _pausePanelController?.Close(restore);
        }

        private void BuildWorldOrderTimerUi()
        {
            if (_worldOrderTimerPanel == null)
            {
                _worldOrderTimerPanel = new HudWorldOrderTimerPanel();
            }

            _worldOrderTimerPanel.BuildIfNeeded();
        }

        private void RefreshWorldOrderTimerUi()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_orderFlowManager == null)
            {
                Services.TryGet(out _orderFlowManager);
            }

            if (_worldOrderTimerPanel == null)
            {
                _worldOrderTimerPanel = new HudWorldOrderTimerPanel();
            }

            _worldOrderTimerPanel.Refresh(_isRunScene, _orderFlowManager, _player);
        }

        private void UpdateWorldOrderTimerTransform()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_worldOrderTimerPanel == null)
            {
                _worldOrderTimerPanel = new HudWorldOrderTimerPanel();
            }

            _worldOrderTimerPanel.UpdateTransform(_isRunScene, _player);
        }

        private void BuildMinimapUi()
        {
            if (_minimapPanel == null)
            {
                _minimapPanel = new HudMinimapPanel();
            }

            _minimapPanel.BuildIfNeeded(_view);
        }

        private void DestroyCircleRing()
        {
            if (_circleRingSprite == null)
            {
                return;
            }

            Texture2D ringTexture = _circleRingSprite.texture;
            Object.Destroy(_circleRingSprite);
            _circleRingSprite = null;
            if (ringTexture != null)
            {
                Object.Destroy(ringTexture);
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
            _currentSpeedKmh = 0f;
            _fuel01 = 1f;
            if (_statusDisplay == null)
            {
                _statusDisplay = new HudStatusDisplay();
            }

            _statusDisplay.Reset();
            _speedMultiplier = 1f;
            _hasFoodState = false;
            _foodOfferId = null;
            _foodTemperature01 = 0f;
            _foodSpill01 = 0f;
            _foodQuality01 = 0f;
            _offerAcceptWindow = false;
            _previewVisible = false;
            _currentOfferId = "A1";
            _currentPickupName = string.Empty;
            _currentDeliveryName = string.Empty;
            _currentOfferReward = 0;
            _currentOfferRemaining = 0f;
            _currentOfferDuration = 0f;
            _offerUiExpireAt = 0d;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = -1;
            _activeOrderCount = 0;
            _sessionBalance = 0;
            _sessionBonus = 0;
            _phonePanel?.ResetRuntimeState();
            MarkPhoneDirty();
            _lastWholeSecond = int.MinValue;
            if (_minimapPanel == null)
            {
                _minimapPanel = new HudMinimapPanel();
            }

            _minimapPanel.ResetRuntimeState();
            _offerPickupNames.Clear();
            _offerDeliveryNames.Clear();
            _offerBaseRewards.Clear();
            _orderCarryingByOffer.Clear();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
                _pickedTrackNameByChoice[i] = null;
            }
        }
    }
}





