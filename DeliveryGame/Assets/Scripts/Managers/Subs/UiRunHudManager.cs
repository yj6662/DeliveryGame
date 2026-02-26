using System;
using System.Text;
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
        private const int MaxMinimapMarkers = 3;
        private const int FuelSegmentCount = 10;

        private const float PhoneWidth = 352f;
        private const float PhoneBaseHeight = 92f;
        private const float PhoneRowHeight = 26f;
        private const float PhonePreviewHeight = 188f;
        private const float PhoneSlideSpeed = 760f;
        private const float ActiveOrderPanelWidth = 372f;
        private const float ActiveOrderPanelBaseHeight = 104f;
        private const float ActiveOrderPanelRowHeight = 60f;
        private const float ActiveOrderPanelPosX = -30f;
        private const float ActiveOrderPanelBasePosY = 228f;
        private const float ActiveOrderPanelGapY = 20f;
        private const float PhoneBasePosX = -34f;
        private const float PhoneBasePosY = 28f;
        private const float PhonePreviewLiftY = 124f;

        private const float MinimapCameraHeight = 80f;
        private const float MinimapOrthographicSize = 70f;
        private const int MinimapTextureSize = 512;
        private const float MinimapPanelSize = 312f; // 208 * 1.5
        private const float MinimapPanelOffset = 36f;
        private const float MinimapInnerPadding = 12f;
        private const float MinimapMarkerPadding = 12f;
        private const int GaugeBarSegments = 10;

        private readonly StringBuilder _builder = new StringBuilder(256);
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

        private GameObject _hudInstance;
        private RunHudView _view;
        private bool _hudLoadRequested;
        private bool _isRunScene;
        private bool _musicChoiceModalOpen;

        private float _scenePollElapsed;
        private float _uiRefreshElapsed;
        private float _minimapRefreshElapsed;
        private int _lastWholeSecond;

        private string _nowPlayingLabel;
        private string _speedLine;
        private string _buffLine;
        private string _synergyLine;
        private string _refuelLine;
        private float _speedMultiplier;
        private float _currentSpeedKmh;
        private float _foodTemperature01;
        private float _foodSpill01;
        private float _foodQuality01;
        private bool _hasFoodState;
        private string _foodOfferId;

        private MotorbikeController _player;

        private readonly bool[] _objectiveMarkerActive = new bool[MaxMinimapMarkers];
        private readonly Vector3[] _objectiveMarkerWorld = new Vector3[MaxMinimapMarkers];
        private readonly OrderPointType[] _objectiveMarkerPointTypes = new OrderPointType[MaxMinimapMarkers];

        private Camera _minimapCamera;
        private RenderTexture _minimapRt;
        private Sprite _circleMaskSprite;
        private Sprite _circleRingSprite;
        private RectTransform _minimapMaskRect;
        private RawImage _minimapRawImage;
        private readonly RectTransform[] _objectiveDotRects = new RectTransform[MaxMinimapMarkers];
        private readonly RectTransform[] _objectiveEdgeRects = new RectTransform[MaxMinimapMarkers];
        private readonly Image[] _objectiveDots = new Image[MaxMinimapMarkers];
        private readonly Image[] _objectiveEdgeBadges = new Image[MaxMinimapMarkers];
        private readonly Text[] _objectiveEdgeTexts = new Text[MaxMinimapMarkers];

        private RectTransform _phonePanelRect;
        private Text _phoneHeaderText;
        private Text _phoneActiveListText;
        private RectTransform _phoneActiveCardsRoot;
        private readonly RectTransform[] _phoneOrderCardRects = new RectTransform[MaxTrackedOrders];
        private readonly Text[] _phoneOrderCardTexts = new Text[MaxTrackedOrders];
        private Image _phonePanelImage;
        private RectTransform _newOfferPanelRect;
        private Image _newOfferPanelImage;
        private Text _newOfferHeaderText;
        private Text _phonePreviewText;
        private Image _newOfferExpiryOverlay;
        private float _phoneCurrentHeight;
        private float _phoneCurrentLift;
        private float _activePanelCurrentHeight;
        private float _activePanelCurrentPosY;
        private bool _phoneDirty;

        private RectTransform _statusGaugeRect;
        private Image _statusGaugeNeedleImage;
        private Text _statusGaugeSpeedText;
        private RectTransform _fuelGaugeRect;
        private Text _fuelGaugeText;
        private readonly Image[] _fuelSegmentImages = new Image[FuelSegmentCount];
        private float _fuel01 = 1f;
        private RectTransform _trackPanelRect;
        private Text _trackHeaderText;
        private Text _trackListText;
        private readonly Image[] _trackStackSlotImages = new Image[ChoiceSlots];

        private string _currentOfferId;
        private string _currentPickupName;
        private string _currentDeliveryName;
        private int _currentOfferReward;
        private float _currentOfferRemaining;
        private float _currentOfferDuration;
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
            Services.TryGet(out _modifierStack);

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
            _currentOfferRemaining = 0f;
            _currentOfferDuration = 0f;
            _foodOfferId = null;
            _hasFoodState = false;
            _orderCarryingByOffer.Clear();
            ClearObjectiveMarkers();
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
            _buffLine = DefaultBuffLabel;
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _pickedGenreByChoice[i] = null;
                _pickedTrackNameByChoice[i] = null;
            }
            _synergyLine = DefaultSynergyLabel;
            _refuelLine = null;
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
            _musicChoiceModalOpen = false;
            _activeOrderCount = 0;
            _offerPickupNames.Clear();
            _offerDeliveryNames.Clear();
            _offerBaseRewards.Clear();
            _orderCarryingByOffer.Clear();
            _phoneCurrentLift = 0f;
            _activePanelCurrentPosY = ActiveOrderPanelBasePosY;
            _phoneDirty = true;
            ClearObjectiveMarkers();

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
            _nowPlayingLabel = "NOW PLAYING: " + trackName;
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
            _synergyLine = DefaultSynergyLabel;
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
            _currentOfferRemaining = 0f;
            RemoveActiveOrder(evt.OfferId);
            _offerPickupNames.Remove(evt.OfferId);
            _offerDeliveryNames.Remove(evt.OfferId);
            _offerBaseRewards.Remove(evt.OfferId);
            _orderCarryingByOffer.Remove(evt.OfferId);
            _phoneDirty = true;
        }
        private void OnOfferAccepted(OfferAccepted evt)
        {
            if (string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                _offerAcceptWindow = false;
                _previewVisible = false;
            }

            string pickupName;
            if (!_offerPickupNames.TryGetValue(evt.OfferId, out pickupName))
            {
                pickupName = _currentPickupName;
            }
            UpsertActiveOrder(evt.OfferId, string.IsNullOrEmpty(pickupName) ? "GO PICKUP" : "GO PICKUP: " + pickupName);
            _orderCarryingByOffer[evt.OfferId] = false;
            _phoneDirty = true;
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
            _phoneDirty = true;
        }

        private void OnOrderCompleted(OrderCompleted evt)
        {
            if (string.Equals(evt.OfferId, _currentOfferId, StringComparison.Ordinal))
            {
                _offerAcceptWindow = false;
                _previewVisible = false;
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
            _phoneDirty = true;
            ApplyCashLabel();
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
            _phoneDirty = true;
        }

        private void OnOrderObjectiveMarkerUpdated(OrderObjectiveMarkerUpdated evt)
        {
            _objectiveMarkerActive[0] = evt.Active;
            _objectiveMarkerWorld[0] = evt.WorldPosition;
            _objectiveMarkerPointTypes[0] = evt.PointType;
            ApplyMarkerColor(0);
            for (int i = 1; i < MaxMinimapMarkers; i++)
            {
                _objectiveMarkerActive[i] = false;
                _objectiveMarkerWorld[i] = Vector3.zero;
                _objectiveMarkerPointTypes[i] = OrderPointType.Pickup;
                ApplyMarkerColor(i);
            }
        }

        private void OnOrderObjectiveMarkersUpdated(OrderObjectiveMarkersUpdated evt)
        {
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                _objectiveMarkerActive[i] = false;
                _objectiveMarkerWorld[i] = Vector3.zero;
                _objectiveMarkerPointTypes[i] = OrderPointType.Pickup;
                ApplyMarkerColor(i);
            }

            int count = evt.Count;
            if (count <= 0)
            {
                return;
            }

            _objectiveMarkerActive[0] = true;
            _objectiveMarkerWorld[0] = evt.WorldPosition0;
            _objectiveMarkerPointTypes[0] = evt.PointType0;
            ApplyMarkerColor(0);

            if (count > 1)
            {
                _objectiveMarkerActive[1] = true;
                _objectiveMarkerWorld[1] = evt.WorldPosition1;
                _objectiveMarkerPointTypes[1] = evt.PointType1;
                ApplyMarkerColor(1);
            }

            if (count > 2)
            {
                _objectiveMarkerActive[2] = true;
                _objectiveMarkerWorld[2] = evt.WorldPosition2;
                _objectiveMarkerPointTypes[2] = evt.PointType2;
                ApplyMarkerColor(2);
            }
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
            _phoneDirty = true;
        }

        private void OnFuelStateChanged(FuelStateChanged evt)
        {
            _fuel01 = Mathf.Clamp01(evt.Fuel01);
            UpdateFuelGaugeVisual();
        }

        private void OnFuelRefuelStateChanged(FuelRefuelStateChanged evt)
        {
            if (evt.IsRefueling)
            {
                _refuelLine = "REFUEL: +" + evt.FuelPerSecond.ToString("0") + "/s  -$" + evt.CostPerSecond.ToString("0") + "/s";
            }
            else
            {
                _refuelLine = null;
            }

            ApplyStatusLines();
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
                ClearObjectiveMarkers();
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
            RefreshTrackPlayerText();
            UpdateSpeedGaugeVisual();
            UpdateFuelGaugeVisual();
            RebuildPhoneTextIfNeeded(true);
        }

        private void DestroyHud()
        {
            ClosePause(true);

            _pauseRootRect = null;
            _pausePanel = null;
            _phonePanelRect = null;
            _phonePanelImage = null;
            _phoneHeaderText = null;
            _phoneActiveListText = null;
            _phoneActiveCardsRoot = null;
            for (int i = 0; i < MaxTrackedOrders; i++)
            {
                _phoneOrderCardRects[i] = null;
                _phoneOrderCardTexts[i] = null;
            }
            _newOfferPanelRect = null;
            _newOfferPanelImage = null;
            _newOfferHeaderText = null;
            _phonePreviewText = null;
            _newOfferExpiryOverlay = null;
            _statusGaugeRect = null;
            _statusGaugeNeedleImage = null;
            _statusGaugeSpeedText = null;
            _fuelGaugeRect = null;
            _fuelGaugeText = null;
            for (int i = 0; i < FuelSegmentCount; i++)
            {
                _fuelSegmentImages[i] = null;
            }
            _trackPanelRect = null;
            _trackHeaderText = null;
            _trackListText = null;
            for (int i = 0; i < ChoiceSlots; i++)
            {
                _trackStackSlotImages[i] = null;
            }

            _minimapMaskRect = null;
            _minimapRawImage = null;
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                _objectiveDotRects[i] = null;
                _objectiveEdgeRects[i] = null;
                _objectiveDots[i] = null;
                _objectiveEdgeBadges[i] = null;
                _objectiveEdgeTexts[i] = null;
            }

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
            _speedLine = "SPD " + speedKmh + " km/h";
            UpdateSpeedGaugeVisual();
            RebuildBuffLineFromModifiers();
            ApplyStatusLines();
        }

        private void ApplyStatusLines()
        {
            if (_view == null)
            {
                return;
            }

            _builder.Clear();
            _builder.Append(_buffLine);
            _builder.Append('\n');
            _builder.Append(_synergyLine);
            if (!string.IsNullOrEmpty(_refuelLine))
            {
                _builder.Append('\n');
                _builder.Append(_refuelLine);
            }
            _view.SetNowPlaying(_builder.ToString());
        }

        private void RebuildBuffLineFromModifiers()
        {
            if (_modifierStack == null)
            {
                Services.TryGet(out _modifierStack);
            }

            if (_modifierStack == null)
            {
                float speedPct = (_speedMultiplier - 1f) * 100f;
                _buffLine = Mathf.Abs(speedPct) < 0.01f ? DefaultBuffLabel : "BUFF: SPD " + speedPct.ToString("+0;-0") + "%";
                return;
            }

            _builder.Clear();
            _builder.Append("BUFF:");
            int appended = 0;

            appended += AppendBuffMul(_builder, "SPD", _modifierStack.GetMul(RunStatId.PlayerMoveSpeedMultiplier), appended);
            appended += AppendBuffMul(_builder, "GRIP", _modifierStack.GetMul(RunStatId.BikeLateralGripMultiplier), appended);
            appended += AppendBuffMul(_builder, "BRAKE", _modifierStack.GetMul(RunStatId.BikeBrakeForceMultiplier), appended);
            appended += AppendBuffMul(_builder, "REWARD", _modifierStack.GetMul(RunStatId.RewardMultiplier), appended);
            appended += AppendBuffMul(_builder, "TEMP", _modifierStack.GetMul(RunStatId.FoodTemperatureDecayMultiplier), appended);
            appended += AppendBuffMul(_builder, "SPILL", _modifierStack.GetMul(RunStatId.FoodSpillGainMultiplier), appended);
            appended += AppendBuffMul(_builder, "TTL", _modifierStack.GetMul(RunStatId.OfferAcceptTtlMultiplier), appended);
            appended += AppendBuffMul(_builder, "RESPAWN", _modifierStack.GetMul(RunStatId.OfferRespawnDelayMultiplier), appended);

            _buffLine = appended <= 0 ? DefaultBuffLabel : _builder.ToString();
        }

        private static int AppendBuffMul(StringBuilder builder, string label, float mul, int appendedCount)
        {
            float pct = (mul - 1f) * 100f;
            if (Mathf.Abs(pct) < 0.01f)
            {
                return 0;
            }

            if (appendedCount > 0)
            {
                builder.Append(" |");
            }

            builder.Append(' ')
                .Append(label)
                .Append(' ')
                .Append(pct.ToString("+0;-0"))
                .Append('%');
            return 1;
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
            _orderCarryingByOffer.Remove(offerId);
        }

        private bool IsOfferCarrying(string offerId)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return false;
            }

            bool isCarrying;
            if (_orderCarryingByOffer.TryGetValue(offerId, out isCarrying))
            {
                return isCarrying;
            }

            return false;
        }

        private bool ShouldShowFoodDetailsForAllCarryingSlots()
        {
            if (!_hasFoodState || _activeOrderCount != 3)
            {
                return false;
            }

            for (int i = 0; i < _activeOrderCount; i++)
            {
                if (!IsOfferCarrying(_activeOrderIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetDetailedOrderCardCountForLayout()
        {
            if (!_hasFoodState || _activeOrderCount <= 0)
            {
                return 0;
            }

            if (ShouldShowFoodDetailsForAllCarryingSlots())
            {
                int carryingCount = 0;
                for (int i = 0; i < _activeOrderCount; i++)
                {
                    if (IsOfferCarrying(_activeOrderIds[i]))
                    {
                        carryingCount++;
                    }
                }

                return carryingCount;
            }

            return 1;
        }

        private void UpdateNewOfferProgressOverlay()
        {
            if (_newOfferExpiryOverlay == null)
            {
                return;
            }

            bool show = _previewVisible && _offerAcceptWindow && _currentOfferDuration > 0.001f;
            if (!show)
            {
                if (_newOfferExpiryOverlay.gameObject.activeSelf)
                {
                    _newOfferExpiryOverlay.gameObject.SetActive(false);
                }

                _newOfferExpiryOverlay.fillAmount = 0f;
                return;
            }

            float normalized = Mathf.Clamp01(_currentOfferRemaining / _currentOfferDuration);
            if (!_newOfferExpiryOverlay.gameObject.activeSelf)
            {
                _newOfferExpiryOverlay.gameObject.SetActive(true);
            }

            _newOfferExpiryOverlay.fillAmount = normalized;
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

            GameObject activePanelObject = new GameObject("ActiveOrdersPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _phonePanelRect = activePanelObject.GetComponent<RectTransform>();
            _phonePanelRect.SetParent(root, false);
            _phonePanelRect.anchorMin = new Vector2(1f, 0f);
            _phonePanelRect.anchorMax = new Vector2(1f, 0f);
            _phonePanelRect.pivot = new Vector2(1f, 0f);
            _activePanelCurrentPosY = ActiveOrderPanelBasePosY;
            _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);
            _activePanelCurrentHeight = ActiveOrderPanelBaseHeight;
            _phoneCurrentLift = 0f;
            _phonePanelRect.sizeDelta = new Vector2(ActiveOrderPanelWidth, _activePanelCurrentHeight);

            _phonePanelImage = activePanelObject.GetComponent<Image>();
            _phonePanelImage.sprite = _view.GetPanelSkinSprite();
            _phonePanelImage.type = _phonePanelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            _phonePanelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            _phonePanelImage.raycastTarget = false;

            GameObject activeScreenObject = new GameObject("Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform activeScreenRect = activeScreenObject.GetComponent<RectTransform>();
            activeScreenRect.SetParent(_phonePanelRect, false);
            AnchorStretch(activeScreenRect, 8f, 8f, 10f, 10f);
            Image activeScreenImage = activeScreenObject.GetComponent<Image>();
            activeScreenImage.color = new Color(0.12f, 0.16f, 0.2f, 0.96f);
            activeScreenImage.raycastTarget = false;

            _phoneHeaderText = CreateText("Header", activeScreenRect, 18, TextAnchor.UpperLeft);
            AnchorStretchTop(_phoneHeaderText.rectTransform, 14f, 14f, 12f, 28f);
            _phoneHeaderText.color = new Color(0.96f, 0.98f, 1f, 1f);

            _phoneActiveListText = CreateText("ActiveList", activeScreenRect, 15, TextAnchor.UpperLeft);
            AnchorStretch(_phoneActiveListText.rectTransform, 14f, 14f, 12f, 44f);
            _phoneActiveListText.verticalOverflow = VerticalWrapMode.Overflow;
            _phoneActiveListText.lineSpacing = 1.06f;
            _phoneActiveListText.gameObject.SetActive(false);

            _phoneActiveCardsRoot = new GameObject("OrderCardsRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            _phoneActiveCardsRoot.SetParent(activeScreenRect, false);
            AnchorStretch(_phoneActiveCardsRoot, 12f, 12f, 10f, 40f);

            for (int i = 0; i < MaxTrackedOrders; i++)
            {
                GameObject cardObject = new GameObject(
                    "OrderCard_" + i.ToString("00"),
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform cardRect = cardObject.GetComponent<RectTransform>();
                cardRect.SetParent(_phoneActiveCardsRoot, false);
                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(1f, 1f);
                cardRect.pivot = new Vector2(0.5f, 1f);
                cardRect.sizeDelta = new Vector2(0f, ActiveOrderPanelRowHeight - 6f);
                cardRect.anchoredPosition = new Vector2(0f, -(i * ActiveOrderPanelRowHeight));

                Image cardImage = cardObject.GetComponent<Image>();
                cardImage.sprite = _view.GetPanelSkinSprite();
                cardImage.type = cardImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                cardImage.color = new Color(0.13f, 0.18f, 0.24f, 0.94f);
                cardImage.raycastTarget = false;

                Text cardText = CreateText("Text", cardRect, 15, TextAnchor.UpperLeft);
                AnchorStretch(cardText.rectTransform, 10f, 10f, 8f, 8f);
                cardText.lineSpacing = 1.05f;
                cardText.color = new Color(0.96f, 0.98f, 1f, 1f);

                _phoneOrderCardRects[i] = cardRect;
                _phoneOrderCardTexts[i] = cardText;
                cardObject.SetActive(false);
            }

            GameObject offerPanelObject = new GameObject("NewOfferPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _newOfferPanelRect = offerPanelObject.GetComponent<RectTransform>();
            _newOfferPanelRect.SetParent(root, false);
            _newOfferPanelRect.anchorMin = new Vector2(1f, 0f);
            _newOfferPanelRect.anchorMax = new Vector2(1f, 0f);
            _newOfferPanelRect.pivot = new Vector2(1f, 0f);
            _newOfferPanelRect.anchoredPosition = new Vector2(PhoneBasePosX, PhoneBasePosY);
            _phoneCurrentHeight = PhoneBaseHeight;
            _newOfferPanelRect.sizeDelta = new Vector2(PhoneWidth, _phoneCurrentHeight);

            _newOfferPanelImage = offerPanelObject.GetComponent<Image>();
            _newOfferPanelImage.sprite = _view.GetPanelSkinSprite();
            _newOfferPanelImage.type = _newOfferPanelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            _newOfferPanelImage.color = new Color(0.09f, 0.1f, 0.14f, 0.97f);
            _newOfferPanelImage.raycastTarget = false;

            GameObject offerScreenObject = new GameObject("Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform offerScreenRect = offerScreenObject.GetComponent<RectTransform>();
            offerScreenRect.SetParent(_newOfferPanelRect, false);
            AnchorStretch(offerScreenRect, 8f, 8f, 10f, 10f);
            Image offerScreenImage = offerScreenObject.GetComponent<Image>();
            offerScreenImage.color = new Color(0.12f, 0.16f, 0.2f, 0.96f);
            offerScreenImage.raycastTarget = false;

            _newOfferHeaderText = CreateText("Header", offerScreenRect, 18, TextAnchor.UpperLeft);
            AnchorStretchTop(_newOfferHeaderText.rectTransform, 14f, 14f, 12f, 28f);
            _newOfferHeaderText.color = new Color(0.96f, 0.98f, 1f, 1f);

            _phonePreviewText = CreateText("Preview", offerScreenRect, 17, TextAnchor.MiddleLeft);
            AnchorStretch(_phonePreviewText.rectTransform, 14f, 14f, 12f, 44f);
            _phonePreviewText.lineSpacing = 1.08f;

            GameObject overlayObject = new GameObject("OfferExpiryOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(offerScreenRect, false);
            AnchorStretch(overlayRect, 0f, 0f, 0f, 0f);
            _newOfferExpiryOverlay = overlayObject.GetComponent<Image>();
            _newOfferExpiryOverlay.color = new Color(0f, 0f, 0f, 0.96f);
            _newOfferExpiryOverlay.type = Image.Type.Filled;
            _newOfferExpiryOverlay.fillMethod = Image.FillMethod.Vertical;
            _newOfferExpiryOverlay.fillOrigin = (int)Image.OriginVertical.Top;
            _newOfferExpiryOverlay.fillAmount = 1f;
            _newOfferExpiryOverlay.raycastTarget = false;
            _newOfferExpiryOverlay.gameObject.SetActive(false);

            _phoneDirty = true;
        }

        private void UpdatePhonePanel(float dt)
        {
            if (_phonePanelRect == null)
            {
                return;
            }

            float activeTarget = ActiveOrderPanelBaseHeight + (_activeOrderCount * ActiveOrderPanelRowHeight) +
                                 (GetDetailedOrderCardCountForLayout() * 52f);
            _activePanelCurrentHeight = Mathf.MoveTowards(_activePanelCurrentHeight, activeTarget, PhoneSlideSpeed * dt);
            _phonePanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _activePanelCurrentHeight);

            if (_newOfferPanelRect == null)
            {
                _activePanelCurrentPosY = Mathf.MoveTowards(_activePanelCurrentPosY, ActiveOrderPanelBasePosY, PhoneSlideSpeed * dt);
                _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);
                return;
            }

            float previewTarget = _previewVisible ? PhonePreviewHeight : PhoneBaseHeight;
            float targetLift = _previewVisible ? PhonePreviewLiftY : 0f;
            _phoneCurrentHeight = Mathf.MoveTowards(_phoneCurrentHeight, previewTarget, PhoneSlideSpeed * dt);
            _phoneCurrentLift = Mathf.MoveTowards(_phoneCurrentLift, targetLift, PhoneSlideSpeed * dt);
            _newOfferPanelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _phoneCurrentHeight);
            _newOfferPanelRect.anchoredPosition = new Vector2(PhoneBasePosX, PhoneBasePosY + _phoneCurrentLift);

            float offerTop = (PhoneBasePosY + _phoneCurrentLift) + _phoneCurrentHeight;
            float activePosTargetY = Mathf.Max(ActiveOrderPanelBasePosY, offerTop + ActiveOrderPanelGapY);
            _activePanelCurrentPosY = Mathf.MoveTowards(_activePanelCurrentPosY, activePosTargetY, PhoneSlideSpeed * dt);
            _phonePanelRect.anchoredPosition = new Vector2(ActiveOrderPanelPosX, _activePanelCurrentPosY);

            UpdateNewOfferProgressOverlay();
        }

        private void RebuildPhoneTextIfNeeded(bool force = false)
        {
            if (!force && !_phoneDirty)
            {
                return;
            }

            if (_phoneHeaderText == null || _phonePreviewText == null)
            {
                return;
            }

            _phoneDirty = false;
            _phoneHeaderText.text = _activeOrderCount > 0 ? "ACTIVE ORDERS  " + _activeOrderCount : "ACTIVE ORDERS  0";
            RefreshActiveOrderCards();
            if (_newOfferHeaderText != null)
            {
                _newOfferHeaderText.text = _previewVisible ? "NEW ORDER" : "INCOMING ORDER";
            }

            if (_previewVisible)
            {
                _builder.Clear();
                _builder.Append("<color=#FFD57A>NEW ORDER</color>");
                if (!string.IsNullOrEmpty(_currentPickupName)) _builder.Append('\n').Append("Pickup  ").Append(_currentPickupName);
                if (!string.IsNullOrEmpty(_currentDeliveryName)) _builder.Append('\n').Append("Dropoff ").Append(_currentDeliveryName);
                _builder.Append('\n').Append("Base Reward  $").Append(_currentOfferReward);
                _builder.Append('\n').Append("Accept TTL   ").Append(_currentOfferRemaining.ToString("0.0")).Append("s");
                if (_hasFoodState)
                {
                    _builder.Append('\n').Append("Temp ").Append(Mathf.RoundToInt(_foodTemperature01 * 100f)).Append("%");
                    _builder.Append("  Spill ").Append(Mathf.RoundToInt(_foodSpill01 * 100f)).Append("%");
                }
                _builder.Append('\n').Append("<color=#9CD2FF>[SPACE]</color> Accept");
                _phonePreviewText.text = _builder.ToString();
                _phonePreviewText.gameObject.SetActive(true);
            }
            else
            {
                _phonePreviewText.text = "Waiting for offer...";
                _phonePreviewText.gameObject.SetActive(true);
            }
        }

        private void RefreshActiveOrderCards()
        {
            if (_phoneOrderCardRects[0] == null || _phoneOrderCardTexts[0] == null)
            {
                if (_phoneActiveListText != null)
                {
                    _phoneActiveListText.gameObject.SetActive(true);
                    _phoneActiveListText.text = _activeOrderCount <= 0 ? "No active orders" : _activeOrderTexts[0];
                }

                return;
            }

            if (_phoneActiveListText != null)
            {
                _phoneActiveListText.gameObject.SetActive(false);
            }

            float y = 0f;
            int visibleCount = _activeOrderCount;
            bool showFallback = visibleCount <= 0;
            bool showFoodOnAllCarrying = ShouldShowFoodDetailsForAllCarryingSlots();
            if (showFallback)
            {
                visibleCount = 1;
            }

            for (int i = 0; i < MaxTrackedOrders; i++)
            {
                RectTransform cardRect = _phoneOrderCardRects[i];
                Text cardText = _phoneOrderCardTexts[i];
                if (cardRect == null || cardText == null)
                {
                    continue;
                }

                bool visible = i < visibleCount;
                cardRect.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                float rowHeight = ActiveOrderPanelRowHeight - 6f;
                _builder.Clear();
                if (showFallback)
                {
                    _builder.Append("No active orders");
                }
                else
                {
                    _builder.Append("#").Append(i + 1).Append("  ").Append(_activeOrderTexts[i]);
                    bool isFoodOrder = false;
                    if (_hasFoodState)
                    {
                        if (showFoodOnAllCarrying)
                        {
                            isFoodOrder = IsOfferCarrying(_activeOrderIds[i]);
                        }
                        else if (!string.IsNullOrEmpty(_foodOfferId))
                        {
                            isFoodOrder = string.Equals(_activeOrderIds[i], _foodOfferId, StringComparison.Ordinal);
                        }
                        else
                        {
                            isFoodOrder = i == 0;
                        }
                    }

                    if (isFoodOrder)
                    {
                        _builder.Append('\n');
                        AppendFoodGaugeLine(_builder, "TEMP", _foodTemperature01, true);
                        _builder.Append('\n');
                        AppendFoodGaugeLine(_builder, "SPILL", _foodSpill01, false);
                        rowHeight += 52f;
                    }
                }

                cardText.text = _builder.ToString();
                cardRect.sizeDelta = new Vector2(0f, rowHeight);
                cardRect.anchoredPosition = new Vector2(0f, -y);
                y += rowHeight + 6f;
            }
        }

        private static void AppendFoodGaugeLine(StringBuilder builder, string label, float value01, bool higherIsBetter)
        {
            float value = Mathf.Clamp01(value01);
            float score = higherIsBetter ? value : (1f - value);
            int fillCount = Mathf.RoundToInt(value * GaugeBarSegments);
            if (fillCount < 0) fillCount = 0;
            if (fillCount > GaugeBarSegments) fillCount = GaugeBarSegments;

            string stateLabel;
            string colorTag;
            if (score >= 0.66f)
            {
                stateLabel = "GOOD";
                colorTag = "#6CFF6C";
            }
            else if (score >= 0.33f)
            {
                stateLabel = "CAUTION";
                colorTag = "#FFD34D";
            }
            else
            {
                stateLabel = "RISK";
                colorTag = "#FF5B5B";
            }

            builder.Append(label).Append(' ').Append('[');
            for (int i = 0; i < GaugeBarSegments; i++)
            {
                builder.Append(i < fillCount ? '|' : '-');
            }

            builder.Append(']')
                .Append(' ')
                .Append("<color=").Append(colorTag).Append('>')
                .Append(stateLabel)
                .Append("</color>")
                .Append(' ')
                .Append(Mathf.RoundToInt(value * 100f)).Append('%');
        }

        private void BuildSpeedGaugeUi()
        {
            if (_view == null || _statusGaugeRect != null)
            {
                return;
            }

            RectTransform root = _view.GetRootRectTransform();
            if (root == null)
            {
                return;
            }

            RectTransform statusPanel = _view.GetBottomCenterPanelRectTransform();

            GameObject gaugeRootGo = new GameObject("StatusSpeedGauge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _statusGaugeRect = gaugeRootGo.GetComponent<RectTransform>();
            _statusGaugeRect.SetParent(root, false);
            _statusGaugeRect.anchorMin = new Vector2(0.5f, 0f);
            _statusGaugeRect.anchorMax = new Vector2(0.5f, 0f);
            _statusGaugeRect.pivot = new Vector2(0.5f, 0.5f);
            _statusGaugeRect.sizeDelta = new Vector2(166f, 166f);

            float gaugeX = -420f;
            float gaugeY = 82f;
            if (statusPanel != null)
            {
                gaugeX = statusPanel.anchoredPosition.x - (statusPanel.sizeDelta.x * 0.5f) - 84f;
                gaugeY = statusPanel.anchoredPosition.y + (statusPanel.sizeDelta.y * 0.5f) - 4f;
            }
            _statusGaugeRect.anchoredPosition = new Vector2(gaugeX, gaugeY);

            Image gaugeBg = gaugeRootGo.GetComponent<Image>();
            gaugeBg.sprite = _view.GetPanelSkinSprite();
            gaugeBg.type = gaugeBg.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            gaugeBg.color = new Color(0.07f, 0.1f, 0.14f, 0.92f);
            gaugeBg.raycastTarget = false;

            Image ring = EnsureMarkerImage(_statusGaugeRect, "GaugeRing", new Color(0.96f, 0.98f, 1f, 0.95f), 124f);
            ring.sprite = _circleRingSprite != null ? _circleRingSprite : CreateCircleRingSprite(128, 4f);
            ring.type = Image.Type.Simple;
            ring.rectTransform.anchoredPosition = Vector2.zero;

            GameObject needleGo = new GameObject("Needle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform needleRect = needleGo.GetComponent<RectTransform>();
            needleRect.SetParent(_statusGaugeRect, false);
            needleRect.anchorMin = new Vector2(0.5f, 0.5f);
            needleRect.anchorMax = new Vector2(0.5f, 0.5f);
            needleRect.pivot = new Vector2(0.08f, 0.5f);
            needleRect.sizeDelta = new Vector2(68f, 4f);
            needleRect.anchoredPosition = Vector2.zero;
            _statusGaugeNeedleImage = needleGo.GetComponent<Image>();
            _statusGaugeNeedleImage.color = new Color(1f, 0.76f, 0.18f, 1f);
            _statusGaugeNeedleImage.raycastTarget = false;

            _statusGaugeSpeedText = CreateText("GaugeSpeedText", _statusGaugeRect, 20, TextAnchor.MiddleCenter);
            _statusGaugeSpeedText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _statusGaugeSpeedText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _statusGaugeSpeedText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _statusGaugeSpeedText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            _statusGaugeSpeedText.rectTransform.sizeDelta = new Vector2(130f, 40f);
            _statusGaugeSpeedText.color = new Color(0.96f, 0.98f, 1f, 1f);

            Text gaugeTitle = CreateText("GaugeTitle", _statusGaugeRect, 14, TextAnchor.MiddleCenter);
            gaugeTitle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            gaugeTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            gaugeTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            gaugeTitle.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            gaugeTitle.rectTransform.sizeDelta = new Vector2(120f, 20f);
            gaugeTitle.text = "SPEED";
            gaugeTitle.color = new Color(1f, 0.86f, 0.28f, 1f);

            Text minLabel = CreateText("GaugeMin", _statusGaugeRect, 12, TextAnchor.MiddleCenter);
            minLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            minLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            minLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            minLabel.rectTransform.anchoredPosition = new Vector2(-56f, -54f);
            minLabel.rectTransform.sizeDelta = new Vector2(28f, 18f);
            minLabel.text = "0";

            Text maxLabel = CreateText("GaugeMax", _statusGaugeRect, 12, TextAnchor.MiddleCenter);
            maxLabel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            maxLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            maxLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            maxLabel.rectTransform.anchoredPosition = new Vector2(56f, -54f);
            maxLabel.rectTransform.sizeDelta = new Vector2(36f, 18f);
            maxLabel.text = "180";
        }

        private void UpdateSpeedGaugeVisual()
        {
            if (_statusGaugeNeedleImage == null || _statusGaugeSpeedText == null)
            {
                return;
            }

            float t = Mathf.Clamp01(_currentSpeedKmh / 180f);
            float angle = Mathf.Lerp(-120f, 120f, t);
            _statusGaugeNeedleImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            _statusGaugeSpeedText.text = Mathf.RoundToInt(_currentSpeedKmh) + "\nkm/h";
        }

        private void BuildFuelGaugeUi()
        {
            if (_view == null || _fuelGaugeRect != null)
            {
                return;
            }

            RectTransform root = _view.GetRootRectTransform();
            if (root == null)
            {
                return;
            }

            RectTransform statusPanel = _view.GetBottomCenterPanelRectTransform();
            if (statusPanel == null)
            {
                return;
            }

            GameObject rootObject = new GameObject("FuelGauge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _fuelGaugeRect = rootObject.GetComponent<RectTransform>();
            _fuelGaugeRect.SetParent(root, false);
            _fuelGaugeRect.anchorMin = new Vector2(0.5f, 0f);
            _fuelGaugeRect.anchorMax = new Vector2(0.5f, 0f);
            _fuelGaugeRect.pivot = new Vector2(0.5f, 0.5f);
            float fuelX = statusPanel.anchoredPosition.x + (statusPanel.sizeDelta.x * 0.5f) + 84f;
            float fuelY = statusPanel.anchoredPosition.y + (statusPanel.sizeDelta.y * 0.5f) - 4f;
            _fuelGaugeRect.anchoredPosition = new Vector2(fuelX, fuelY);
            _fuelGaugeRect.sizeDelta = new Vector2(178f, 84f);

            Image bg = rootObject.GetComponent<Image>();
            bg.sprite = _view.GetPanelSkinSprite();
            bg.type = bg.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = new Color(0.07f, 0.1f, 0.14f, 0.92f);
            bg.raycastTarget = false;

            _fuelGaugeText = CreateText("FuelLabel", _fuelGaugeRect, 14, TextAnchor.MiddleCenter);
            _fuelGaugeText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _fuelGaugeText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _fuelGaugeText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _fuelGaugeText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            _fuelGaugeText.rectTransform.sizeDelta = new Vector2(146f, 20f);
            _fuelGaugeText.color = new Color(1f, 0.9f, 0.35f, 1f);
            _fuelGaugeText.text = "FUEL";

            RectTransform segmentsRoot = new GameObject("Segments", typeof(RectTransform)).GetComponent<RectTransform>();
            segmentsRoot.SetParent(_fuelGaugeRect, false);
            segmentsRoot.anchorMin = new Vector2(0f, 0f);
            segmentsRoot.anchorMax = new Vector2(1f, 1f);
            segmentsRoot.offsetMin = new Vector2(12f, 10f);
            segmentsRoot.offsetMax = new Vector2(-12f, -28f);

            for (int i = 0; i < FuelSegmentCount; i++)
            {
                Image segment = EnsureMarkerImage(segmentsRoot, "Segment_" + i, new Color(0.15f, 0.18f, 0.24f, 1f), 14f);
                RectTransform rt = segment.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(i * 15f, 0f);
                rt.sizeDelta = new Vector2(12f, 18f);
                segment.sprite = _view.GetPanelSkinSprite();
                segment.type = segment.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                segment.raycastTarget = false;
                _fuelSegmentImages[i] = segment;
            }
        }

        private void UpdateFuelGaugeVisual()
        {
            if (_fuelGaugeRect == null)
            {
                return;
            }

            float scaled = _fuel01 * FuelSegmentCount;
            for (int i = 0; i < FuelSegmentCount; i++)
            {
                Image segment = _fuelSegmentImages[i];
                if (segment == null)
                {
                    continue;
                }

                float fill = Mathf.Clamp01(scaled - i);
                float width = Mathf.Lerp(3f, 13f, fill);
                float height = Mathf.Lerp(12f, 20f, fill);
                segment.rectTransform.sizeDelta = new Vector2(width, height);

                if (fill <= 0.001f)
                {
                    segment.color = new Color(0.15f, 0.18f, 0.24f, 0.65f);
                }
                else if (_fuel01 <= 0.25f)
                {
                    segment.color = new Color(1f, 0.35f, 0.25f, 1f);
                }
                else if (_fuel01 <= 0.5f)
                {
                    segment.color = new Color(1f, 0.78f, 0.25f, 1f);
                }
                else
                {
                    segment.color = new Color(0.32f, 0.93f, 0.62f, 1f);
                }
            }

            if (_fuelGaugeText != null)
            {
                _fuelGaugeText.text = "FUEL  " + Mathf.RoundToInt(_fuel01 * 100f) + "%";
            }
        }

        private void BuildTrackPlayerUi()
        {
            if (_view == null || _trackPanelRect != null)
            {
                return;
            }

            RectTransform root = _view.GetRootRectTransform();
            if (root == null)
            {
                return;
            }

            GameObject panelObject = new GameObject("TrackStackPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _trackPanelRect = panelObject.GetComponent<RectTransform>();
            _trackPanelRect.SetParent(root, false);
            _trackPanelRect.anchorMin = new Vector2(0.5f, 1f);
            _trackPanelRect.anchorMax = new Vector2(0.5f, 1f);
            _trackPanelRect.pivot = new Vector2(0.5f, 1f);
            _trackPanelRect.anchoredPosition = new Vector2(0f, -18f);
            _trackPanelRect.sizeDelta = new Vector2(520f, 116f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = _view.GetPanelSkinSprite();
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.08f, 0.12f, 0.16f, 0.9f);
            panelImage.raycastTarget = false;

            Text iconText = CreateText("Icon", _trackPanelRect, 22, TextAnchor.MiddleCenter);
            iconText.rectTransform.anchorMin = new Vector2(0f, 1f);
            iconText.rectTransform.anchorMax = new Vector2(0f, 1f);
            iconText.rectTransform.pivot = new Vector2(0f, 1f);
            iconText.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            iconText.rectTransform.sizeDelta = new Vector2(26f, 22f);
            iconText.text = "\u266B";
            iconText.color = new Color(1f, 0.84f, 0.24f, 1f);

            _trackHeaderText = CreateText("Header", _trackPanelRect, 17, TextAnchor.MiddleLeft);
            _trackHeaderText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _trackHeaderText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _trackHeaderText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _trackHeaderText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            _trackHeaderText.rectTransform.sizeDelta = new Vector2(-46f, 22f);
            _trackHeaderText.text = "TRACK STACK";
            _trackHeaderText.color = new Color(0.97f, 0.78f, 0.2f, 1f);

            _trackListText = CreateText("Tracks", _trackPanelRect, 16, TextAnchor.UpperLeft);
            _trackListText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _trackListText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _trackListText.rectTransform.offsetMin = new Vector2(14f, 14f);
            _trackListText.rectTransform.offsetMax = new Vector2(-14f, -32f);
            _trackListText.verticalOverflow = VerticalWrapMode.Overflow;

            for (int i = 0; i < ChoiceSlots; i++)
            {
                Image slotImage = EnsureMarkerImage(_trackPanelRect, "StackSlot_" + (i + 1), new Color(1f, 1f, 1f, 0.16f), 48f);
                RectTransform slotRect = slotImage.rectTransform;
                slotRect.anchorMin = new Vector2(1f, 1f);
                slotRect.anchorMax = new Vector2(1f, 1f);
                slotRect.pivot = new Vector2(1f, 1f);
                slotRect.anchoredPosition = new Vector2(-(14f + (i * 54f)), -12f);
                slotRect.sizeDelta = new Vector2(46f, 8f);
                slotImage.sprite = _view.GetPanelSkinSprite();
                slotImage.type = slotImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                slotImage.color = new Color(1f, 1f, 1f, 0.16f);
                _trackStackSlotImages[i] = slotImage;
            }
        }

        private void RefreshTrackPlayerText()
        {
            if (_trackListText == null)
            {
                return;
            }

            _builder.Clear();
            int stackCount = 0;
            for (int i = 0; i < ChoiceSlots; i++)
            {
                string trackName = _pickedTrackNameByChoice[i];
                if (string.IsNullOrEmpty(trackName))
                {
                    continue;
                }

                if (stackCount > 0)
                {
                    _builder.Append('\n');
                }
                _builder.Append(i + 1).Append(". ").Append(trackName);
                stackCount++;
            }

            if (stackCount <= 0)
            {
                _trackListText.text = "-";
                for (int i = 0; i < ChoiceSlots; i++)
                {
                    Image slot = _trackStackSlotImages[i];
                    if (slot == null)
                    {
                        continue;
                    }
                    slot.color = new Color(1f, 1f, 1f, 0.16f);
                }
                return;
            }

            _trackListText.text = _builder.ToString();
            for (int i = 0; i < ChoiceSlots; i++)
            {
                Image slot = _trackStackSlotImages[i];
                if (slot == null)
                {
                    continue;
                }

                slot.color = i < stackCount
                    ? new Color(1f, 0.83f, 0.2f, 0.92f)
                    : new Color(1f, 1f, 1f, 0.16f);
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
            _pauseRootRect.anchoredPosition = new Vector2(-20f, -20f);
            _pauseRootRect.sizeDelta = new Vector2(252f, 212f);

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
            panelRect.anchoredPosition = new Vector2(MinimapPanelOffset, MinimapPanelOffset);
            panelRect.sizeDelta = new Vector2(MinimapPanelSize, MinimapPanelSize);

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

            if (_circleRingSprite == null)
            {
                _circleRingSprite = CreateCircleRingSprite(128, 3f);
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
            _minimapMaskRect.offsetMin = new Vector2(MinimapInnerPadding, MinimapInnerPadding);
            _minimapMaskRect.offsetMax = new Vector2(-MinimapInnerPadding, -MinimapInnerPadding);

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

            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                Color markerColor = GetMarkerColorForType(_objectiveMarkerPointTypes[i]);
                float markerSize = i == 0 ? 16f : 13f;

                Image dot = EnsureMarkerImage(overlay, "ObjectiveDot_" + i, markerColor, markerSize);
                dot.gameObject.SetActive(false);
                _objectiveDots[i] = dot;
                _objectiveDotRects[i] = dot.rectTransform;

                Image edgeBadge = EnsureMarkerImage(overlay, "ObjectiveEdgeBadge_" + i, markerColor, i == 0 ? 18f : 16f);
                edgeBadge.sprite = _circleRingSprite;
                edgeBadge.type = Image.Type.Simple;
                edgeBadge.gameObject.SetActive(false);
                _objectiveEdgeBadges[i] = edgeBadge;
                _objectiveEdgeRects[i] = edgeBadge.rectTransform;

                Text edgeText = EnsureMarkerText(edgeBadge.rectTransform, "Arrow", ">", new Color(0f, 0f, 0f, 1f), 14);
                edgeText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.sizeDelta = new Vector2(18f, 18f);
                edgeText.rectTransform.anchoredPosition = Vector2.zero;
                edgeText.gameObject.SetActive(false);
                _objectiveEdgeTexts[i] = edgeText;

                ApplyMarkerColor(i);
            }

            Text playerArrow = EnsureMarkerText(overlay, "PlayerArrow", "^", new Color(0.25f, 1f, 0.8f, 1f), 24);
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
                for (int i = 0; i < MaxMinimapMarkers; i++)
                {
                    SetMarkerHidden(i);
                }
                return;
            }

            Transform p = _player.transform;
            float yaw = p.eulerAngles.y;
            _minimapCamera.transform.SetPositionAndRotation(
                p.position + Vector3.up * MinimapCameraHeight,
                Quaternion.Euler(90f, yaw, 0f));

            float uiRadius = Mathf.Min(_minimapMaskRect.rect.width, _minimapMaskRect.rect.height) * 0.5f - MinimapMarkerPadding;
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                if (_objectiveDots[i] == null || _objectiveEdgeRects[i] == null ||
                    _objectiveEdgeTexts[i] == null || _objectiveEdgeBadges[i] == null)
                {
                    continue;
                }

                if (!_objectiveMarkerActive[i])
                {
                    SetMarkerHidden(i);
                    continue;
                }

                ApplyMarkerColor(i);

                Vector3 delta = _objectiveMarkerWorld[i] - p.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.0001f)
                {
                    SetMarkerHidden(i);
                    continue;
                }

                Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * delta;
                Vector2 flat = new Vector2(local.x, local.z);
                Vector2 uiPos = flat * (uiRadius / Mathf.Max(1f, _minimapCamera.orthographicSize));

                if (uiPos.magnitude <= uiRadius * 0.86f)
                {
                    _objectiveDots[i].gameObject.SetActive(true);
                    _objectiveEdgeBadges[i].gameObject.SetActive(false);
                    _objectiveEdgeTexts[i].gameObject.SetActive(false);
                    _objectiveDotRects[i].anchoredPosition = uiPos;
                    continue;
                }

                Vector2 dir = uiPos.normalized;
                _objectiveDots[i].gameObject.SetActive(false);
                _objectiveEdgeBadges[i].gameObject.SetActive(true);
                _objectiveEdgeTexts[i].gameObject.SetActive(true);
                _objectiveEdgeRects[i].anchoredPosition = dir * (uiRadius * 1.02f);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _objectiveEdgeRects[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void SetMarkerHidden(int index)
        {
            if (index < 0 || index >= MaxMinimapMarkers)
            {
                return;
            }

            if (_objectiveDots[index] != null)
            {
                _objectiveDots[index].gameObject.SetActive(false);
            }

            if (_objectiveEdgeBadges[index] != null)
            {
                _objectiveEdgeBadges[index].gameObject.SetActive(false);
            }

            if (_objectiveEdgeTexts[index] != null)
            {
                _objectiveEdgeTexts[index].gameObject.SetActive(false);
            }
        }

        private Color GetMarkerColorForType(OrderPointType pointType)
        {
            if (pointType == OrderPointType.Delivery)
            {
                // Delivery marker: cyan/blue
                return new Color(0.22f, 0.78f, 1f, 0.98f);
            }

            // Pickup/restaurant marker: yellow-orange
            return new Color(1f, 0.84f, 0.2f, 0.98f);
        }

        private void ApplyMarkerColor(int index)
        {
            if (index < 0 || index >= MaxMinimapMarkers)
            {
                return;
            }

            Color markerColor = GetMarkerColorForType(_objectiveMarkerPointTypes[index]);
            if (_objectiveDots[index] != null)
            {
                _objectiveDots[index].color = markerColor;
            }

            if (_objectiveEdgeBadges[index] != null)
            {
                _objectiveEdgeBadges[index].color = markerColor;
            }
        }

        private void ClearObjectiveMarkers()
        {
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                _objectiveMarkerActive[i] = false;
                _objectiveMarkerWorld[i] = Vector3.zero;
                _objectiveMarkerPointTypes[i] = OrderPointType.Pickup;
                SetMarkerHidden(i);
            }
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
                // no-op
            }
            else
            {
                Texture2D texture = _circleMaskSprite.texture;
                Object.Destroy(_circleMaskSprite);
                _circleMaskSprite = null;
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }

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
            _nowPlayingLabel = DefaultNowPlayingLabel;
            _speedLine = "SPEED: 0 km/h";
            _currentSpeedKmh = 0f;
            _fuel01 = 1f;
            _buffLine = DefaultBuffLabel;
            _synergyLine = DefaultSynergyLabel;
            _refuelLine = null;
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
            _activeOrderCount = 0;
            _sessionBalance = 0;
            _sessionBonus = 0;
            _phoneCurrentHeight = PhoneBaseHeight;
            _phoneCurrentLift = 0f;
            _activePanelCurrentHeight = ActiveOrderPanelBaseHeight;
            _activePanelCurrentPosY = ActiveOrderPanelBasePosY;
            _phoneDirty = true;
            _lastWholeSecond = int.MinValue;
            ClearObjectiveMarkers();
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

        private static Sprite CreateCircleRingSprite(int size, float thickness)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "MinimapCircleRing";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float outerRadius = center - 1f;
            float innerRadius = Mathf.Max(0f, outerRadius - Mathf.Max(1f, thickness));
            float outerSq = outerRadius * outerRadius;
            float innerSq = innerRadius * innerRadius;
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
                    float d2 = dx * dx + dy * dy;
                    pixels[idx++] = (d2 <= outerSq && d2 >= innerSq) ? fill : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}




