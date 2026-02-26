using DeliveryRun;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Subs
{
    public sealed class UiRunHudManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private const float TimeUpdateInterval = 0.1f;
        private const string BuffInactiveLabel = "BUFF ACTIVE: -";
        private const string DefaultNowPlayingLabel = "NOW PLAYING: -";
        private const string DefaultDeliveryLabel = "DELIVER TO: -";
        private const string DefaultCashLabel = "CASH: $0";
        private const string DefaultOfferId = "A1";
        private const string DefaultFoodLine = "TEMP -- | SPILL -- | RATING --";

        private UiPrefabCatalogSO _catalog;
        private AddressablesService _addressables;
        private AudioManager _audioManager;
        private MusicLibraryService _musicLibrary;
        private GameObject _hudInstance;
        private RunHudView _view;
        private bool _isRunScene;
        private bool _hudLoadRequested;
        private float _scenePollElapsed;
        private float _timeUpdateElapsed;
        private int _lastWholeSecond;
        private string _nowPlayingLabel;
        private string _deliveryLabel;
        private float _speedMultiplier;
        private string _buffLine;
        private string _foodLine;
        private bool _hasFoodSample;
        private bool _hasRatingSample;
        private float _temp01;
        private float _spill01;
        private float _quality01;
        private float _ratingValue;
        private string _offerId;
        private string _pickupName;
        private string _deliveryName;
        private int _offerReward;
        private float _offerRemainingSeconds;
        private bool _offerAcceptWindow;
        private int _sessionBalance;

        public override string Name => nameof(UiRunHudManager);
        public override int InitOrder => 36;

        protected override void OnInitialize()
        {
            _catalog = UiPrefabCatalogLoader.LoadOrNull();
            Services.TryGet(out _addressables);
            Services.TryGet(out _audioManager);
            Services.TryGet(out _musicLibrary);
            _nowPlayingLabel = DefaultNowPlayingLabel;
            _deliveryLabel = DefaultDeliveryLabel;
            _speedMultiplier = 1f;
            _buffLine = BuffInactiveLabel;
            _foodLine = DefaultFoodLine;
            _hasFoodSample = false;
            _hasRatingSample = false;
            _temp01 = 0f;
            _spill01 = 0f;
            _quality01 = 0f;
            _ratingValue = 0f;
            _lastWholeSecond = int.MinValue;
            _offerId = DefaultOfferId;
            _offerReward = 0;
            _offerRemainingSeconds = 0f;
            _offerAcceptWindow = false;
            _sessionBalance = 0;

            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
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
            Subs.Add<SessionBalanceChanged>(Events, OnSessionBalanceChanged);
            Subs.Add<FoodStateTicked>(Events, OnFoodTicked);
            Subs.Add<RatingChanged>(Events, OnRatingChanged);

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

            _timeUpdateElapsed += unscaledDeltaTime;
            if (_timeUpdateElapsed < TimeUpdateInterval)
            {
                return;
            }

            _timeUpdateElapsed = 0f;
            RefreshTimeLabel();
        }

        protected override void OnShutdown()
        {
            DestroyHud();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To != SceneNames.RunScene)
            {
                DestroyHud();
                _isRunScene = false;
                _offerAcceptWindow = false;
            }
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            string displayName = string.Empty;
            if (_musicLibrary == null)
            {
                Services.TryGet(out _musicLibrary);
            }

            if (_musicLibrary != null && !string.IsNullOrEmpty(evt.TrackId))
            {
                DeliveryRun.Music.MusicTrackSO track;
                if (_musicLibrary.TryGetTrack(evt.TrackId, out track) && track != null)
                {
                    displayName = track.DisplayName;
                }
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = ToTrackName(evt.OptionIndex);
            }

            _nowPlayingLabel = "NOW PLAYING: " + displayName;
            if (_view != null)
            {
                _view.SetNowPlaying(_nowPlayingLabel);
            }
        }

        private void OnSpeedMultiplierChanged(PlayerMoveSpeedMultiplierChanged evt)
        {
            _speedMultiplier = evt.Multiplier;
            ApplyBuffLabel();
        }

        private void OnModifiersCleared(RunModifiersCleared evt)
        {
            _speedMultiplier = 1f;
            ApplyBuffLabel();
        }

        private void OnFoodTicked(FoodStateTicked evt)
        {
            _hasFoodSample = true;
            _temp01 = evt.Temperature01;
            _spill01 = evt.Spill01;
            _quality01 = evt.Quality01;
            RebuildFoodLine();
        }

        private void OnRatingChanged(RatingChanged evt)
        {
            _hasRatingSample = true;
            _ratingValue = evt.Rating;
            RebuildFoodLine();
        }

        private void OnOfferSpawned(OfferSpawned evt)
        {
            _offerId = string.IsNullOrEmpty(evt.OfferId) ? DefaultOfferId : evt.OfferId;
            _pickupName = evt.PickupName;
            _deliveryName = evt.DeliveryName;
            _offerReward = evt.Reward;
            _offerRemainingSeconds = evt.TtlSeconds;
            _offerAcceptWindow = true;
            ApplyOfferLabel();
        }

        private void OnOfferTicked(OfferTicked evt)
        {
            if (!string.Equals(evt.OfferId, _offerId, System.StringComparison.Ordinal))
            {
                return;
            }

            _offerRemainingSeconds = evt.RemainingSeconds;
            if (_offerAcceptWindow)
            {
                ApplyOfferLabel();
            }
        }

        private void OnOfferExpired(OfferExpired evt)
        {
            if (!string.Equals(evt.OfferId, _offerId, System.StringComparison.Ordinal))
            {
                return;
            }

            _offerAcceptWindow = false;
            SetDeliveryLabel("OFFER EXPIRED. New offer soon...");
        }

        private void OnOfferAccepted(OfferAccepted evt)
        {
            if (!string.Equals(evt.OfferId, _offerId, System.StringComparison.Ordinal))
            {
                return;
            }

            _offerAcceptWindow = false;
            if (string.IsNullOrEmpty(_pickupName))
            {
                SetDeliveryLabel("GO PICKUP");
                return;
            }

            SetDeliveryLabel("GO PICKUP: " + _pickupName);
        }

        private void OnOrderPickupReached(OrderPickupReached evt)
        {
            if (!string.Equals(evt.OfferId, _offerId, System.StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrEmpty(_deliveryName))
            {
                SetDeliveryLabel("DELIVER TO");
                return;
            }

            SetDeliveryLabel("DELIVER TO: " + _deliveryName);
        }

        private void OnOrderCompleted(OrderCompleted evt)
        {
            if (!string.Equals(evt.OfferId, _offerId, System.StringComparison.Ordinal))
            {
                return;
            }

            _offerAcceptWindow = false;
            SetDeliveryLabel("ORDER COMPLETE! +$" + evt.Reward);
        }

        private void OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            if (string.IsNullOrEmpty(evt.Text))
            {
                return;
            }

            SetDeliveryLabel(evt.Text);
        }

        private void OnSessionBalanceChanged(SessionBalanceChanged evt)
        {
            _sessionBalance = evt.Balance;
            ApplyCashLabel();
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool shouldBeRunScene = sceneName == SceneNames.RunScene;
            if (_isRunScene == shouldBeRunScene)
            {
                if (_isRunScene)
                {
                    EnsureHudExists();
                }

                return;
            }

            _isRunScene = shouldBeRunScene;
            if (_isRunScene)
            {
                _lastWholeSecond = int.MinValue;
                _hasFoodSample = false;
                _hasRatingSample = false;
                _temp01 = 0f;
                _spill01 = 0f;
                _quality01 = 0f;
                _ratingValue = 0f;
                _foodLine = DefaultFoodLine;
                _buffLine = BuffInactiveLabel;
                EnsureHudExists();
                return;
            }

            DestroyHud();
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
            _view = _hudInstance.GetComponent<RunHudView>();
            if (_view == null)
            {
                Debug.LogError("[UiRunHudManager] RunHUD prefab missing RunHudView component.");
                Object.Destroy(_hudInstance);
                _hudInstance = null;
                return;
            }

            _view.SetNowPlaying(_nowPlayingLabel);
            _view.SetDelivery(_deliveryLabel);
            _view.SetFocusButtonAction(OnFocusAcceptClicked);
            RefreshBuffLabelFromService();
            ApplyCashLabel();
            _lastWholeSecond = int.MinValue;
            _timeUpdateElapsed = TimeUpdateInterval;
            RefreshTimeLabel();
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
                ReleaseHudObject(hudObject);
                return;
            }

            _hudInstance = hudObject;
            _view = hudObject.GetComponent<RunHudView>();
            if (_view == null)
            {
                Debug.LogError("[UiRunHudManager] Addressables RunHUD prefab missing RunHudView component.");
                ReleaseHudObject(hudObject);
                _hudInstance = null;
                return;
            }

            _view.SetNowPlaying(_nowPlayingLabel);
            _view.SetDelivery(_deliveryLabel);
            _view.SetFocusButtonAction(OnFocusAcceptClicked);
            RefreshBuffLabelFromService();
            ApplyCashLabel();
            _lastWholeSecond = int.MinValue;
            _timeUpdateElapsed = TimeUpdateInterval;
            RefreshTimeLabel();
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

            float remainingSeconds = runSessionManager.RemainingSeconds;
            int wholeSeconds = Mathf.CeilToInt(remainingSeconds);
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
            int seconds = wholeSeconds - (minutes * 60);
            _view.SetTimeLabel("Time: " + minutes.ToString("00") + ":" + seconds.ToString("00"));
        }

        private void DestroyHud()
        {
            if (_view != null)
            {
                _view.SetFocusButtonAction(null);
            }

            if (_hudInstance != null)
            {
                ReleaseHudObject(_hudInstance);
            }

            _view = null;
            _hudInstance = null;
            _lastWholeSecond = int.MinValue;
            _timeUpdateElapsed = 0f;
            _hudLoadRequested = false;
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

            PublishAcceptOfferRequest();
        }

        private void OnFocusAcceptClicked()
        {
            PublishAcceptOfferRequest();
        }

        private void PublishAcceptOfferRequest()
        {
            if (!_offerAcceptWindow)
            {
                return;
            }

            _offerAcceptWindow = false;
            TryPlayUiClick();
            Events.Publish(new AcceptOfferRequested { OfferId = _offerId });
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

        private void RefreshBuffLabelFromService()
        {
            ModifierStackService stack;
            if (Services.TryGet(out stack) && stack != null)
            {
                _speedMultiplier = stack.GetMul(RunStatId.PlayerMoveSpeedMultiplier);
            }
            else
            {
                _speedMultiplier = 1f;
            }

            ApplyBuffLabel();
        }

        private void ApplyCashLabel()
        {
            if (_view == null)
            {
                return;
            }

            if (_sessionBalance <= 0)
            {
                _view.SetCash(DefaultCashLabel);
                return;
            }

            _view.SetCash("CASH: $" + _sessionBalance.ToString("N0"));
        }

        private void ApplyBuffLabel()
        {
            float pct = (_speedMultiplier - 1f) * 100f;
            if (Mathf.Abs(pct) < 0.01f)
            {
                _buffLine = BuffInactiveLabel;
            }
            else
            {
                _buffLine = "BUFF ACTIVE: SPD " + pct.ToString("+0;-0") + "%";
            }

            UpdateBuffLabelCombined();
        }

        private void RebuildFoodLine()
        {
            if (!_hasFoodSample && !_hasRatingSample)
            {
                _foodLine = DefaultFoodLine;
                UpdateBuffLabelCombined();
                return;
            }

            if (_hasFoodSample)
            {
                int tempPct = Mathf.RoundToInt(_temp01 * 100f);
                int spillPct = Mathf.RoundToInt(_spill01 * 100f);
                _foodLine = "TEMP " + tempPct + "% | SPILL " + spillPct + "% | Q " + _quality01.ToString("0.00");
            }
            else
            {
                _foodLine = "TEMP -- | SPILL -- | Q --";
            }

            if (_hasRatingSample)
            {
                _foodLine += " | R " + _ratingValue.ToString("0.0");
            }
            else
            {
                _foodLine += " | R --";
            }

            UpdateBuffLabelCombined();
        }

        private void UpdateBuffLabelCombined()
        {
            if (_view == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_buffLine))
            {
                _view.SetBuffLabel(_foodLine);
                return;
            }

            if (string.IsNullOrEmpty(_foodLine))
            {
                _view.SetBuffLabel(_buffLine);
                return;
            }

            _view.SetBuffLabel(_buffLine + "\n" + _foodLine);
        }

        private void ApplyOfferLabel()
        {
            if (!_offerAcceptWindow)
            {
                return;
            }

            string offerText =
                "OFFER: Press [F] to Accept (" + _offerRemainingSeconds.ToString("0.0") + "s) | Reward $" + _offerReward;
            SetDeliveryLabel(offerText);
        }

        private void SetDeliveryLabel(string text)
        {
            _deliveryLabel = string.IsNullOrEmpty(text) ? DefaultDeliveryLabel : text;
            if (_view != null)
            {
                _view.SetDelivery(_deliveryLabel);
            }
        }

        private static string ToTrackName(int optionIndex)
        {
            if (optionIndex == 0)
            {
                return "NITRO BEAT";
            }

            if (optionIndex == 1)
            {
                return "CHILL CRUISE";
            }

            if (optionIndex == 2)
            {
                return "RISK BASS";
            }

            return "UNKNOWN";
        }

        private void ReleaseHudObject(GameObject hudObject)
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
    }
}
