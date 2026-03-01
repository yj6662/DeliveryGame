using DeliveryRun;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.UI.Features
{
    internal sealed partial class RunHudUiFeature
    {
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
            Subs.Add<FoodQualityComputed>(Events, OnFoodQualityComputed);
            Subs.Add<FuelStateChanged>(Events, OnFuelStateChanged);
            Subs.Add<FuelRefuelStateChanged>(Events, OnFuelRefuelStateChanged);

            HandleSceneChanged(SceneManager.GetActiveScene().name);
        }

        private void TickRunHudModule(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(ref _scenePollElapsed, ScenePollInterval, unscaledDeltaTime))
            {
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
            _phoneCoordinator.TickOfferCountdown(Clock.Now);
            UpdatePhonePanel(unscaledDeltaTime);
            UpdateWorldOrderTimerTransform();
            UpdateWorldRewardPopupTransform();

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
                _worldOverlayCoordinator.RefreshMinimap(ref _player);
            }
        }

        private void ShutdownRunHudModule()
        {
            _corePanelsCoordinator.ClosePause(true);
            DestroyHud();
            _worldOverlayCoordinator.Cleanup();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (SceneNames.IsRunSceneLike(evt.To))
            {
                return;
            }

            _isRunScene = false;
            _phoneCoordinator.ResetForSceneExit();
            _completedOrderQualityByOffer.Clear();
            _worldOverlayCoordinator.ResetRuntimeState();
            _musicChoiceModalOpen = false;
            _corePanelsCoordinator.ClosePause(true);
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
            ClearMusicChoiceSelection();
            _fuel01 = 1f;

            _musicChoiceModalOpen = false;
            _phoneCoordinator.ResetForRunSession();
            _completedOrderQualityByOffer.Clear();
            _worldOverlayCoordinator.ResetRuntimeState();

            if (_view != null)
            {
                ApplyStatusLines();
                ApplyCashLabel();
                _corePanelsCoordinator.RefreshTrack(_pickedTrackNameByChoice);
                _corePanelsCoordinator.SetSpeed(_currentSpeedKmh);
                _corePanelsCoordinator.SetFuel(_fuel01);
                RebuildPhoneTextIfNeeded();
            }
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
                _phoneCoordinator.ResetForSceneExit();
                _completedOrderQualityByOffer.Clear();
                _worldOverlayCoordinator.ResetRuntimeState();
                _corePanelsCoordinator.ClosePause(true);
                DestroyHud();
                return;
            }

            _lastWholeSecond = int.MinValue;
            _uiRefreshElapsed = UiRefreshInterval;
            _minimapRefreshElapsed = MinimapRefreshInterval;
            _worldOverlayCoordinator.ResetRuntimeState();
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
            _corePanelsCoordinator.BuildGaugeUi(_view, _fuel01);
            _corePanelsCoordinator.BuildTrackUi(_view);
            _corePanelsCoordinator.BuildPauseUi(_view, () => Events.Publish(new ReturnToLobbyRequested()), TryPlayUiClick);
            BuildWorldOrderTimerUi();
            _corePanelsCoordinator.RefreshTrack(_pickedTrackNameByChoice);
            _corePanelsCoordinator.SetSpeed(_currentSpeedKmh);
            _corePanelsCoordinator.SetFuel(_fuel01);
            RebuildPhoneTextIfNeeded(true);
            RefreshWorldOrderTimerUi();
        }

        private void DestroyHud()
        {
            _corePanelsCoordinator.ClosePause(true);
            _corePanelsCoordinator.Cleanup();
            _phoneCoordinator.Cleanup();
            _worldOverlayCoordinator.Cleanup();

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
    }
}
