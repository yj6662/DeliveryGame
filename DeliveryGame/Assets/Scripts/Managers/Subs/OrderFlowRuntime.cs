using System;
using System.Collections.Generic;
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
    internal sealed partial class OrderFlowRuntime
    {
        private const float ObjectiveTickInterval = 0.25f;
        private const float ScenePollInterval = 0.25f;
        private const int MaxActiveOrders = 3;

        private readonly ServiceRegistry _services;
        private readonly EventBus _events;
        private readonly GameClock _clock;

        private readonly List<OrderRuntimeActiveOrder> _activeOrders = new List<OrderRuntimeActiveOrder>(8);
        private readonly List<OrderObjectivePublisher.ObjectiveOrderSnapshot> _objectiveSnapshots =
            new List<OrderObjectivePublisher.ObjectiveOrderSnapshot>(8);

        private readonly OrderFoodSelectionService _foodSelectionService = new OrderFoodSelectionService();
        private readonly OrderAnchorSelectionService _anchorSelectionService = new OrderAnchorSelectionService();
        private readonly OrderObjectivePublisher _objectivePublisher = new OrderObjectivePublisher();
        private readonly OrderRoadSideLocator _roadSideLocator = new OrderRoadSideLocator();
        private readonly OrderInteractPointFactory _interactPointFactory = new OrderInteractPointFactory();
        private readonly OrderSettlementService _settlementService = new OrderSettlementService();
        private readonly OrderDeadlineTracker _deadlineTracker = new OrderDeadlineTracker();
        private readonly OrderPendingOfferRuntime _pendingOfferRuntime = new OrderPendingOfferRuntime();

        private EconomyService _economy;
        private FoodStateConfigSO _foodCfg;
        private ModifierStackService _stack;
        private MetaProgressionService _meta;

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
        private int _lastMusicOptionIndex;

        internal OrderFlowRuntime(ServiceRegistry services, EventBus events, GameClock clock)
        {
            _services = services;
            _events = events;
            _clock = clock;
        }

        internal void Initialize()
        {
            _economy = new EconomyService();
            _services.Register(_economy);

            _foodCfg = Resources.Load<FoodStateConfigSO>("Bootstrap/FoodStateConfig");
            if (_foodCfg == null)
            {
                Debug.LogError("[OrderFlowManager] Missing config: Resources/Bootstrap/FoodStateConfig");
            }

            _services.TryGet(out _stack);
            _services.TryGet(out _meta);

            _anchorCatalog = new OrderAnchorCatalog(_foodSelectionService);
            _interactController = new OrderInteractPointController(_interactPointFactory, _roadSideLocator);
            _offerSpawner = new OrderOfferSpawner(_foodSelectionService, _anchorSelectionService, _pendingOfferRuntime);

            _runActive = false;
            _isRunScene = false;
            _objectiveTickAccum = 0f;
            _scenePollAccum = 0f;
            _ordersCompleted = 0;
            _lastMusicOptionIndex = -1;

            HandleSceneChanged(SceneManager.GetActiveScene().name);
            if (_isRunScene)
            {
                RunSessionManager runSessionManager;
                if (_services.TryGet(out runSessionManager) && runSessionManager != null && runSessionManager.HasActiveRun)
                {
                    BeginRunSession();
                }
            }
        }

        internal void Tick(float unscaledDeltaTime)
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

            _deadlineTracker.TickDeadlines(_activeOrders, _clock.Now, FailOrderByTimeout);
            _interactController.EnsureInteractPoints(_activeOrders, unscaledDeltaTime, _roadQuery, _services, _events);
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

        internal void Shutdown()
        {
            CleanupRunState();
            _runActive = false;
            _isRunScene = false;
            _anchorCatalog?.Clear();
        }

        internal int CopyActiveOrderTimerViewsNonAlloc(ActiveOrderTimerView[] destination)
        {
            return _deadlineTracker.CopyTimerViewsNonAlloc(_activeOrders, destination, _clock.Now);
        }

        internal void OnRunStarted(DomainRunSessionStarted evt) => BeginRunSession();

        internal void OnRunEnded(DomainRunSessionEnded evt)
        {
            if (_runActive)
            {
                PublishRunReport();
            }

            CleanupRunState();
            _runActive = false;
        }

        internal void OnAcceptRequested(AcceptOfferRequested evt)
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

            if (_clock.Now > pendingOffer.EndTime)
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
                AcceptedAt = _clock.Now,
                PickedUpAt = -1d,
                RestaurantAnchor = pendingOffer.RestaurantAnchor,
                DestinationAnchor = pendingOffer.DestinationAnchor,
                FoodId = pendingOffer.FoodId,
                FoodName = pendingOffer.FoodName,
                FoodTempDecayMultiplier = pendingOffer.FoodTempDecayMultiplier,
                FoodSpillGainMultiplier = pendingOffer.FoodSpillGainMultiplier,
                DeliveryLimitSeconds = pendingOffer.DeliveryLimitSeconds,
                DeliveryDeadlineAt = _clock.Now + pendingOffer.DeliveryLimitSeconds,
                FoodIsSeafood = pendingOffer.FoodIsSeafood,
                RegionId = pendingOffer.RegionId
            };

            _activeOrders.Add(order);
            _interactController.SpawnPickup(order, _roadQuery, _services, _events);
            _events.Publish(new OfferAccepted { OfferId = order.OfferId });

            _offerSpawner.ClearPendingOffer();
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        internal void OnOrderInteractRequested(OrderInteractRequested evt)
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
                order.PickedUpAt = _clock.Now;
                _interactController.DespawnPickup(order);
                _events.Publish(new OrderPickupReached
                {
                    OfferId = order.OfferId,
                    FoodId = order.FoodId,
                    FoodName = order.FoodName,
                    TemperatureDecayMultiplier = Mathf.Max(0.05f, order.FoodTempDecayMultiplier),
                    SpillGainMultiplier = Mathf.Max(0.05f, order.FoodSpillGainMultiplier)
                });
                _interactController.SpawnDelivery(order, _roadQuery, _services, _events);
                PublishPrimaryObjectiveUpdate();
                return;
            }

            if (order.Stage == OrderStage.Carrying && evt.PointType == OrderPointType.Delivery)
            {
                CompleteOrderSuccess(index, order);
            }
        }

        internal void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _lastMusicOptionIndex = evt.OptionIndex;
        }

        internal void OnMusicChoiceModalStateChanged(MusicChoiceModalStateChanged evt)
        {
            _offerSpawner?.OnMusicChoiceModalStateChanged(evt.IsOpen, _clock.Now);
        }

        internal void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (SceneNames.IsRunSceneLike(evt.To))
            {
                return;
            }

            _isRunScene = false;
            CleanupSceneRefsAndPoints();
        }

        internal void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }
    }
}
