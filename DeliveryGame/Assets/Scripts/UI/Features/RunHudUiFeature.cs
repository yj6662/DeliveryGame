using System;
using DeliveryRun;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
using DeliveryRun.Music;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;


namespace DeliveryRun.UI.Features
{
    internal sealed class RunHudUiFeature : UiFeatureBase
    {
        protected override void OnInitialize()
        {
            InitializeRunHudModule();
        }
        protected override void OnTick(float unscaledDeltaTime)
        {
            TickRunHudModule(unscaledDeltaTime);
        }
        protected override void OnShutdown()
        {
            ShutdownRunHudModule();
        }

        private const float ScenePollInterval = 0.25f;
        private const float UiRefreshInterval = 0.1f;
        private const float MinimapRefreshInterval = 0.05f;

        private const int ChoiceSlots = 3;
        private const int MaxTrackedOrders = 8;

        private readonly string[] _pickedGenreByChoice = new string[ChoiceSlots];
        private readonly string[] _pickedTrackNameByChoice = new string[ChoiceSlots];
        private readonly HudPhoneState _phoneState = new HudPhoneState(MaxTrackedOrders);

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

        private MotorbikeController _player;

        private Sprite _circleRingSprite;
        private HudMinimapPanel _minimapPanel;

        private HudPhonePanel _phonePanel;

        private float _fuel01 = 1f;
        private HudGaugePanel _gaugePanel;
        private HudTrackPlayerPanel _trackPlayerPanel;

        private HudWorldOrderTimerPanel _worldOrderTimerPanel;

        private int _sessionBalance;

        private HudPausePanel _pausePanelController;
        private HudStatusDisplay _statusDisplay;

        internal bool IsPauseMenuOpen => _pausePanelController != null && _pausePanelController.IsOpen;

        private void InitializeRunHudModule()
        {
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

        private void TickRunHudModule(float unscaledDeltaTime)
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

        private void ShutdownRunHudModule()
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
            _phoneState.ResetForSceneExit();
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
            _speedMultiplier = 1f;
            _currentSpeedKmh = 0f;
            _statusDisplay?.Reset();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
                _pickedTrackNameByChoice[i] = null;
            }
            _fuel01 = 1f;

            _musicChoiceModalOpen = false;
            _phoneState.ResetForRunSession();
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
            _phoneState.OnMusicChoiceModalStateChanged(evt.IsOpen, Clock.Now);
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
            _phoneState.OnOfferSpawned(evt, Clock.Now);
            MarkPhoneDirty();
        }

        private void OnOfferTicked(OfferTicked evt)
        {
            if (_phoneState.OnOfferTicked(evt))
            {
                MarkPhoneDirty();
            }
        }

        private void OnOfferExpired(OfferExpired evt)
        {
            if (_phoneState.OnOfferExpired(evt))
            {
                MarkPhoneDirty();
            }
        }
        private void OnOfferAccepted(OfferAccepted evt)
        {
            _phoneState.OnOfferAccepted(evt);
            MarkPhoneDirty();
        }

        private void OnOrderPickupReached(OrderPickupReached evt)
        {
            _phoneState.OnOrderPickupReached(evt);
            MarkPhoneDirty();
        }

        private void OnOrderCompleted(OrderCompleted evt)
        {
            _phoneState.OnOrderCompleted(evt);
            MarkPhoneDirty();
            ApplyCashLabel();
        }

        private void OnOrderTimedOut(OrderTimedOut evt)
        {
            _phoneState.OnOrderTimedOut(evt);
            MarkPhoneDirty();
        }

        private void OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            if (_phoneState.OnOrderObjectiveUpdated(evt))
            {
                MarkPhoneDirty();
            }
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
            _phoneState.OnFoodStateTicked(evt);
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
                _phoneState.ResetForSceneExit();
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

            if (_hudLoadRequested)
            {
                return;
            }

            _hudLoadRequested = true;
            Ui.InstantiateRunHud(OnHudInstantiated);
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

            Ui.ReleaseUiInstance(hudObject);
        }

        private void HandleOfferAcceptInput()
        {
            if (!RuntimeInput.WasOfferAcceptPressedThisFrame())
            {
                return;
            }

            string offerId;
            if (!_phoneState.TryConsumeOfferAccept(_musicChoiceModalOpen, out offerId))
            {
                return;
            }

            TryPlayUiClick();
            Events.Publish(new AcceptOfferRequested { OfferId = offerId });
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
            _statusDisplay?.ApplyCash(_view, _sessionBalance, _phoneState.SessionBonus);
        }

        private void MarkPhoneDirty()
        {
            _phonePanel?.SetDirty();
        }

        private HudPhonePanel.PhoneViewState BuildPhoneViewState()
        {
            return _phoneState.BuildViewState();
        }

        private void UpdateOfferCountdownFromClock()
        {
            if (_phoneState.TickOfferCountdown(Clock.Now))
            {
                MarkPhoneDirty();
            }
        }

        private void BuildPhoneUi()
        {
            if (_phonePanel == null)
            {
                _phonePanel = new HudPhonePanel(_phoneState.ActiveOrders, _phoneState.CarryingByOffer);
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
            Ui.PlayUiClick();
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
            _phoneState.ResetAll();
            _sessionBalance = 0;
            _phonePanel?.ResetRuntimeState();
            MarkPhoneDirty();
            _lastWholeSecond = int.MinValue;
            if (_minimapPanel == null)
            {
                _minimapPanel = new HudMinimapPanel();
            }

            _minimapPanel.ResetRuntimeState();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
                _pickedTrackNameByChoice[i] = null;
            }
        }
    }
}
