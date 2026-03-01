using System;
using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class OrderFlowManager : SubManagerBase
    {
        private const float ObjectiveTickInterval = 0.25f;
        private const float ScenePollInterval = 0.25f;
        private const int MaxActiveOrders = 3;

        private readonly List<OrderRuntimeActiveOrder> _activeOrders = new List<OrderRuntimeActiveOrder>(8);
        private readonly List<OrderObjectivePublisher.ObjectiveOrderSnapshot> _objectiveSnapshots =
            new List<OrderObjectivePublisher.ObjectiveOrderSnapshot>(8);

        private EconomyService _economy;
        private FoodStateConfigSO _foodCfg;
        private ModifierStackService _stack;
        private MetaProgressionService _meta;

        private readonly OrderFoodSelectionService _foodSelectionService = new OrderFoodSelectionService();
        private readonly OrderAnchorSelectionService _anchorSelectionService = new OrderAnchorSelectionService();
        private readonly OrderObjectivePublisher _objectivePublisher = new OrderObjectivePublisher();
        private readonly OrderRoadSideLocator _roadSideLocator = new OrderRoadSideLocator();
        private readonly OrderInteractPointFactory _interactPointFactory = new OrderInteractPointFactory();
        private readonly OrderSettlementService _settlementService = new OrderSettlementService();
        private readonly OrderDeadlineTracker _deadlineTracker = new OrderDeadlineTracker();
        private readonly OrderPendingOfferRuntime _pendingOfferRuntime = new OrderPendingOfferRuntime();

        private OrderAnchorCatalog _anchorCatalog;
        private OrderInteractPointController _interactController;
        private OrderOfferSpawner _offerSpawner;

        private MotorbikeController _player;
        private RoadQueryManager _roadQuery;

        private bool _runActive;
        private bool _isRunScene;
        private float _objectiveTickAccum;
        private float _scenePollAccum;
        private int _ordersCompleted;
        private int _lastMusicOptionIndex = -1;

        public override string Name => nameof(OrderFlowManager);
        public override int InitOrder => 70;

        protected override void OnInitialize()
        {
            _economy = new EconomyService();
            Services.Register(_economy);

            _foodCfg = Resources.Load<FoodStateConfigSO>("Bootstrap/FoodStateConfig");
            if (_foodCfg == null)
            {
                Debug.LogError("[OrderFlowManager] Missing config: Resources/Bootstrap/FoodStateConfig");
            }

            Services.TryGet(out _stack);
            Services.TryGet(out _meta);

            _anchorCatalog = new OrderAnchorCatalog(_foodSelectionService);
            _interactController = new OrderInteractPointController(_interactPointFactory, _roadSideLocator);
            _offerSpawner = new OrderOfferSpawner(_foodSelectionService, _anchorSelectionService, _pendingOfferRuntime);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<AcceptOfferRequested>(Events, OnAcceptRequested);
            Subs.Add<OrderInteractRequested>(Events, OnOrderInteractRequested);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
            Subs.Add<MusicChoiceModalStateChanged>(Events, OnMusicChoiceModalStateChanged);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            HandleSceneChanged(SceneManager.GetActiveScene().name);
            if (_isRunScene)
            {
                RunSessionManager runSessionManager;
                if (Services.TryGet(out runSessionManager) && runSessionManager != null && runSessionManager.HasActiveRun)
                {
                    BeginRunSession();
                }
            }
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum >= ScenePollInterval)
            {
                _scenePollAccum = 0f;
                HandleSceneChanged(SceneManager.GetActiveScene().name);
            }

            if (!_runActive || !_isRunScene)
            {
                return;
            }

            EnsureRuntimeReferences();

            if (_offerSpawner.HasPendingOffer)
            {
                TickPendingOffer(unscaledDeltaTime);
            }

            _deadlineTracker.TickDeadlines(_activeOrders, Clock.Now, FailOrderByTimeout);
            _interactController.EnsureInteractPoints(_activeOrders, unscaledDeltaTime, _roadQuery, Services, Events);
            _interactController.TryHandleInput(_activeOrders, _player);

            if (_activeOrders.Count > 0)
            {
                _objectiveTickAccum += unscaledDeltaTime;
                if (_objectiveTickAccum >= ObjectiveTickInterval)
                {
                    _objectiveTickAccum = 0f;
                    PublishPrimaryObjectiveUpdate();
                }
            }
        }

        protected override void OnShutdown()
        {
            CleanupRunState();
            _runActive = false;
            _isRunScene = false;
            _anchorCatalog.Clear();
        }

        // --- Event handlers (thin delegates) ---

        private void OnRunStarted(DomainRunSessionStarted evt) => BeginRunSession();

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            if (_runActive)
            {
                PublishRunReport();
            }

            CleanupRunState();
            _runActive = false;
        }

        private void OnAcceptRequested(AcceptOfferRequested evt)
        {
            if (!_runActive || !_offerSpawner.HasPendingOffer)
            {
                return;
            }

            OrderPendingOffer pendingOffer = _offerSpawner.CurrentOffer;
            if (!string.Equals(evt.OfferId, pendingOffer.OfferId, StringComparison.Ordinal))
            {
                return;
            }

            if (Clock.Now > pendingOffer.EndTime)
            {
                ExpirePendingOffer();
                return;
            }

            if (_activeOrders.Count >= MaxActiveOrders)
            {
                Debug.LogWarning("[OrderFlowManager] Max active order limit reached.");
                return;
            }

            OrderRuntimeActiveOrder order = new OrderRuntimeActiveOrder
            {
                OrderId = pendingOffer.OrderId,
                OfferId = pendingOffer.OfferId,
                PickupName = pendingOffer.PickupName,
                DeliveryName = pendingOffer.DeliveryName,
                BaseReward = pendingOffer.Reward,
                Stage = OrderStage.AwaitPickup,
                AcceptedAt = Clock.Now,
                PickedUpAt = -1d,
                RestaurantAnchor = pendingOffer.RestaurantAnchor,
                DestinationAnchor = pendingOffer.DestinationAnchor,
                FoodId = pendingOffer.FoodId,
                FoodName = pendingOffer.FoodName,
                FoodTempDecayMultiplier = pendingOffer.FoodTempDecayMultiplier,
                FoodSpillGainMultiplier = pendingOffer.FoodSpillGainMultiplier,
                DeliveryLimitSeconds = pendingOffer.DeliveryLimitSeconds,
                DeliveryDeadlineAt = Clock.Now + pendingOffer.DeliveryLimitSeconds,
                FoodIsSeafood = pendingOffer.FoodIsSeafood,
                RegionId = pendingOffer.RegionId
            };

            _activeOrders.Add(order);
            _interactController.SpawnPickup(order, _roadQuery, Services, Events);
            Events.Publish(new OfferAccepted { OfferId = order.OfferId });

            _offerSpawner.ClearPendingOffer();
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        private void OnOrderInteractRequested(OrderInteractRequested evt)
        {
            if (!_runActive)
            {
                return;
            }

            int index = FindOrderIndex(evt.OrderId);
            if (index < 0)
            {
                return;
            }

            OrderRuntimeActiveOrder order = _activeOrders[index];
            if (order.Stage == OrderStage.AwaitPickup && evt.PointType == OrderPointType.Pickup)
            {
                order.Stage = OrderStage.Carrying;
                order.PickedUpAt = Clock.Now;
                _interactController.DespawnPickup(order);
                Events.Publish(new OrderPickupReached
                {
                    OfferId = order.OfferId,
                    FoodId = order.FoodId,
                    FoodName = order.FoodName,
                    TemperatureDecayMultiplier = Mathf.Max(0.05f, order.FoodTempDecayMultiplier),
                    SpillGainMultiplier = Mathf.Max(0.05f, order.FoodSpillGainMultiplier)
                });
                _interactController.SpawnDelivery(order, _roadQuery, Services, Events);
                PublishPrimaryObjectiveUpdate();
                return;
            }

            if (order.Stage == OrderStage.Carrying && evt.PointType == OrderPointType.Delivery)
            {
                CompleteOrderSuccess(index, order);
            }
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _lastMusicOptionIndex = evt.OptionIndex;
        }

        private void OnMusicChoiceModalStateChanged(MusicChoiceModalStateChanged evt)
        {
            _offerSpawner.OnMusicChoiceModalStateChanged(evt.IsOpen, Clock.Now);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (SceneNames.IsRunSceneLike(evt.To))
            {
                return;
            }

            _isRunScene = false;
            CleanupSceneRefsAndPoints();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }

        // --- Session lifecycle ---

        private void BeginRunSession()
        {
            _runActive = true;
            _ordersCompleted = 0;
            _lastMusicOptionIndex = -1;
            _objectiveTickAccum = 0f;
            _interactController.RetryAccum = 0f;

            CleanupRunState();
            _anchorCatalog.Clear();
            _offerSpawner.Reset();

            _economy.ResetSession();
            Events.Publish(new SessionBalanceChanged
            {
                Balance = 0,
                Delta = 0,
                Reason = "run_start"
            });

            EnsureRuntimeReferences();
            DoSpawnOffer();
        }

        // --- Offer management ---

        private void DoSpawnOffer()
        {
            _offerSpawner.SpawnOffer(
                _runActive,
                _activeOrders,
                _anchorCatalog,
                ResolveCurrentRunRegionId(),
                ResolveModifierStack(),
                Clock,
                Events);

            if (!_offerSpawner.HasPendingOffer)
            {
                ScheduleRespawnIfNeeded();
            }
        }

        private void TickPendingOffer(float unscaledDeltaTime)
        {
            string offerId;
            float remainingSeconds;
            OrderOfferSpawner.TickResult result =
                _offerSpawner.TickPendingOffer(unscaledDeltaTime, Clock, out offerId, out remainingSeconds);

            if (result == OrderOfferSpawner.TickResult.Expired)
            {
                ExpirePendingOffer();
                return;
            }

            if (result == OrderOfferSpawner.TickResult.Ticked)
            {
                Events.Publish(new OfferTicked
                {
                    OfferId = offerId,
                    RemainingSeconds = remainingSeconds
                });
            }
        }

        private void ExpirePendingOffer()
        {
            string expiredOfferId = _offerSpawner.ClearPendingOffer();
            if (expiredOfferId != null)
            {
                Events.Publish(new OfferExpired { OfferId = expiredOfferId });
            }

            ScheduleRespawnIfNeeded();
        }

        private void ScheduleRespawnIfNeeded()
        {
            _offerSpawner.ScheduleRespawnIfNeeded(
                _runActive, _isRunScene, _activeOrders,
                ResolveModifierStack(), Clock,
                OnRespawnDue);
        }

        private void OnRespawnDue()
        {
            if (!_runActive || !_isRunScene || _offerSpawner.HasPendingOffer || _activeOrders.Count >= MaxActiveOrders)
            {
                return;
            }

            DoSpawnOffer();
        }

        // --- Order completion ---

        private void CompleteOrderSuccess(int orderIndex, OrderRuntimeActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            _ordersCompleted++;
            _settlementService.ProcessSuccess(
                Services, Events, Clock, _economy, _foodCfg,
                ResolveModifierStack(),
                order.OfferId, order.BaseReward, order.AcceptedAt, order.PickedUpAt);

            _interactController.DespawnPickup(order);
            _interactController.DespawnDelivery(order);
            _activeOrders.RemoveAt(orderIndex);
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        private void FailOrderByTimeout(int orderIndex, OrderRuntimeActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            _interactController.DespawnPickup(order);
            _interactController.DespawnDelivery(order);

            if (_activeOrders.Count > orderIndex)
            {
                _activeOrders.RemoveAt(orderIndex);
            }

            _settlementService.ProcessTimeout(Services, Events, order.OfferId, order.FoodName, order.DeliveryLimitSeconds);
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        // --- Objective publishing ---

        private void PublishPrimaryObjectiveUpdate()
        {
            _objectiveSnapshots.Clear();
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                OrderRuntimeActiveOrder order = _activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                _objectiveSnapshots.Add(new OrderObjectivePublisher.ObjectiveOrderSnapshot
                {
                    OfferId = order.OfferId,
                    PickupName = order.PickupName,
                    DeliveryName = order.DeliveryName,
                    IsCarrying = order.Stage == OrderStage.Carrying,
                    PickupInteract = order.PickupInteract,
                    DeliveryInteract = order.DeliveryInteract
                });
            }

            bool hasPlayer = _player != null;
            Vector3 playerPosition = hasPlayer ? _player.transform.position : Vector3.zero;
            _objectivePublisher.Publish(Events, _objectiveSnapshots, hasPlayer, playerPosition);
        }

        private void PublishNoObjectives()
        {
            _objectiveSnapshots.Clear();
            _objectivePublisher.Publish(Events, _objectiveSnapshots, false, Vector3.zero);
        }

        // --- Public API ---

        public int CopyActiveOrderTimerViewsNonAlloc(ActiveOrderTimerView[] destination)
        {
            return _deadlineTracker.CopyTimerViewsNonAlloc(_activeOrders, destination, Clock.Now);
        }

        // --- Helpers ---

        private void EnsureRuntimeReferences()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_roadQuery == null)
            {
                Services.TryGet(out _roadQuery);
            }

            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            _anchorCatalog.EnsureReady(_meta, Services);
        }

        private int FindOrderIndex(int orderId)
        {
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                if (_activeOrders[i].OrderId == orderId)
                {
                    return i;
                }
            }

            return -1;
        }

        private string ResolveCurrentRunRegionId()
        {
            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            if (_meta != null && !string.IsNullOrEmpty(_meta.SelectedRegionId))
            {
                if (_meta.IsRegionUnlocked(_meta.SelectedRegionId))
                {
                    return _meta.SelectedRegionId;
                }
            }

            return "central";
        }

        private ModifierStackService ResolveModifierStack()
        {
            if (_stack == null)
            {
                Services.TryGet(out _stack);
            }

            return _stack;
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool isRun = SceneNames.IsRunSceneLike(sceneName);
            if (_isRunScene == isRun)
            {
                return;
            }

            _isRunScene = isRun;
            if (!_isRunScene)
            {
                CleanupSceneRefsAndPoints();
                return;
            }

            EnsureRuntimeReferences();
        }

        private void PublishRunReport()
        {
            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            RatingManager ratingManager = null;
            Services.TryGet(out ratingManager);

            RunReport report = new RunReport
            {
                EarnedCash = _economy.SessionBalance,
                OrdersCompleted = _ordersCompleted,
                MusicOptionIndex = _lastMusicOptionIndex,
                EndRating = ratingManager != null ? ratingManager.CurrentRating : 0f,
                RegionId = _meta != null ? _meta.SelectedRegionId : "central"
            };

            Events.Publish(new RunReportReady { Report = report });
        }

        private void CleanupSceneRefsAndPoints()
        {
            _offerSpawner.CancelRespawn(Clock);
            _interactController.DespawnAll(_activeOrders);
            PublishNoObjectives();
            _activeOrders.Clear();
            _offerSpawner.ClearPendingOffer();
            _interactController.RetryAccum = 0f;
            _player = null;
            _anchorCatalog.Clear();
        }

        private void CleanupRunState()
        {
            _offerSpawner.CancelRespawn(Clock);
            _interactController.DespawnAll(_activeOrders);
            PublishNoObjectives();
            _activeOrders.Clear();
            _offerSpawner.ClearPendingOffer();
            _objectiveTickAccum = 0f;
            _interactController.RetryAccum = 0f;
        }
    }
}
