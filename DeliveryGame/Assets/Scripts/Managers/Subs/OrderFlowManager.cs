using System;
using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Delivery.World;
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
        private enum OrderStage
        {
            AwaitPickup = 0,
            Carrying = 1
        }

        private sealed class ActiveOrder
        {
            public int OrderId;
            public string OfferId;
            public string PickupName;
            public string DeliveryName;
            public int BaseReward;
            public OrderStage Stage;
            public double AcceptedAt;
            public double PickedUpAt;
            public OrderInteractPoint PickupInteract;
            public OrderInteractPoint DeliveryInteract;
            public OrderBuildingAnchor RestaurantAnchor;
            public OrderBuildingAnchor DestinationAnchor;
            public string FoodId;
            public string FoodName;
            public float FoodTempDecayMultiplier;
            public float FoodSpillGainMultiplier;
            public float DeliveryLimitSeconds;
            public double DeliveryDeadlineAt;
            public bool FoodIsSeafood;
            public string RegionId;
        }

        private struct PendingOffer
        {
            public bool Active;
            public int OrderId;
            public string OfferId;
            public string PickupName;
            public string DeliveryName;
            public int Reward;
            public double EndTime;
            public OrderBuildingAnchor RestaurantAnchor;
            public OrderBuildingAnchor DestinationAnchor;
            public string FoodId;
            public string FoodName;
            public float FoodTempDecayMultiplier;
            public float FoodSpillGainMultiplier;
            public float DeliveryLimitSeconds;
            public bool FoodIsSeafood;
            public string RegionId;
        }

        public struct ActiveOrderTimerView
        {
            public string OfferId;
            public string FoodName;
            public float RemainingSeconds;
            public float LimitSeconds;
            public bool IsSeafood;
            public bool IsCarrying;
        }

        private const int BaseReward = 250;
        private const float BaseOfferTtlSeconds = 5f;
        private const float BaseRespawnDelaySeconds = 4.5f;
        private const float OfferTickInterval = 0.1f;
        private const float ObjectiveTickInterval = 0.25f;
        private const float ScenePollInterval = 0.25f;
        private const float InteractRadius = 4f;
        private const float InteractSpawnRetryInterval = 0.35f;
        private const int MaxActiveOrders = 3;
        private const float DefaultDeliveryLimitSeconds = 120f;

        private readonly List<ActiveOrder> _activeOrders = new List<ActiveOrder>(8);
        private readonly List<OrderBuildingAnchor> _restaurantAnchors = new List<OrderBuildingAnchor>(32);
        private readonly List<OrderBuildingAnchor> _destinationAnchors = new List<OrderBuildingAnchor>(96);
        private readonly List<OrderBuildingAnchor> _orderAnchors = new List<OrderBuildingAnchor>(128);
        private readonly Dictionary<string, OrderBuildingAnchor> _foodRestaurantMap =
            new Dictionary<string, OrderBuildingAnchor>(16);
        private readonly List<OrderObjectivePublisher.ObjectiveOrderSnapshot> _objectiveSnapshots =
            new List<OrderObjectivePublisher.ObjectiveOrderSnapshot>(8);

        private EconomyService _economy;
        private FoodStateConfigSO _foodCfg;
        private readonly OrderFoodSelectionService _foodSelectionService = new OrderFoodSelectionService();
        private readonly OrderAnchorSelectionService _anchorSelectionService = new OrderAnchorSelectionService();
        private readonly OrderObjectivePublisher _objectivePublisher = new OrderObjectivePublisher();
        private readonly OrderRoadSideLocator _roadSideLocator = new OrderRoadSideLocator();
        private readonly OrderBuildingRoleAssigner _buildingRoleAssigner = new OrderBuildingRoleAssigner();
        private readonly OrderInteractPointFactory _interactPointFactory = new OrderInteractPointFactory();
        private readonly OrderSettlementService _settlementService = new OrderSettlementService();
        private ModifierStackService _stack;
        private MetaProgressionService _meta;

        private MotorbikeController _player;
        private RoadQueryManager _roadQuery;

        private bool _runActive;
        private bool _isRunScene;
        private bool _missingAnchorLogged;
        private bool _offerPauseActive;
        private bool _anchorCatalogReady;
        private double _offerPauseStartedAt;

        private PendingOffer _pendingOffer;

        private float _offerTickAccum;
        private float _objectiveTickAccum;
        private float _scenePollAccum;
        private float _interactSpawnRetryAccum;

        private int _pendingRespawnHandle = -1;
        private int _ordersCompleted;
        private int _lastMusicOptionIndex = -1;
        private int _nextOrderId = 1;
        private int _nextOfferSerial = 1;

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

            if (_pendingOffer.Active)
            {
                TickPendingOffer(unscaledDeltaTime);
            }

            TickActiveOrderDeadlines();
            EnsureActiveOrderInteractPoints(unscaledDeltaTime);
            TryHandleOrderInteractInput();

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
            ClearAnchorCatalog();
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            BeginRunSession();
        }

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
            if (!_runActive || !_pendingOffer.Active)
            {
                return;
            }

            if (!string.Equals(evt.OfferId, _pendingOffer.OfferId, StringComparison.Ordinal))
            {
                return;
            }

            if (Clock.Now > _pendingOffer.EndTime)
            {
                ExpirePendingOffer();
                return;
            }

            if (_activeOrders.Count >= MaxActiveOrders)
            {
                Debug.LogWarning("[OrderFlowManager] Max active order limit reached.");
                return;
            }

            ActiveOrder order = new ActiveOrder
            {
                OrderId = _pendingOffer.OrderId,
                OfferId = _pendingOffer.OfferId,
                PickupName = _pendingOffer.PickupName,
                DeliveryName = _pendingOffer.DeliveryName,
                BaseReward = _pendingOffer.Reward,
                Stage = OrderStage.AwaitPickup,
                AcceptedAt = Clock.Now,
                PickedUpAt = -1d,
                RestaurantAnchor = _pendingOffer.RestaurantAnchor,
                DestinationAnchor = _pendingOffer.DestinationAnchor,
                FoodId = _pendingOffer.FoodId,
                FoodName = _pendingOffer.FoodName,
                FoodTempDecayMultiplier = _pendingOffer.FoodTempDecayMultiplier,
                FoodSpillGainMultiplier = _pendingOffer.FoodSpillGainMultiplier,
                DeliveryLimitSeconds = _pendingOffer.DeliveryLimitSeconds,
                DeliveryDeadlineAt = Clock.Now + _pendingOffer.DeliveryLimitSeconds,
                FoodIsSeafood = _pendingOffer.FoodIsSeafood,
                RegionId = _pendingOffer.RegionId
            };

            _activeOrders.Add(order);
            SpawnPickupInteractPoint(order);
            Events.Publish(new OfferAccepted { OfferId = order.OfferId });

            ClearPendingOffer();
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

            ActiveOrder order = _activeOrders[index];
            if (order.Stage == OrderStage.AwaitPickup && evt.PointType == OrderPointType.Pickup)
            {
                order.Stage = OrderStage.Carrying;
                order.PickedUpAt = Clock.Now;
                DespawnPickupInteractPoint(order);
                Events.Publish(new OrderPickupReached
                {
                    OfferId = order.OfferId,
                    FoodId = order.FoodId,
                    FoodName = order.FoodName,
                    TemperatureDecayMultiplier = Mathf.Max(0.05f, order.FoodTempDecayMultiplier),
                    SpillGainMultiplier = Mathf.Max(0.05f, order.FoodSpillGainMultiplier)
                });
                SpawnDeliveryInteractPoint(order);
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
            if (!_pendingOffer.Active)
            {
                _offerPauseActive = false;
                _offerPauseStartedAt = 0d;
                return;
            }

            if (evt.IsOpen)
            {
                if (_offerPauseActive)
                {
                    return;
                }

                _offerPauseActive = true;
                _offerPauseStartedAt = Clock.Now;
                return;
            }

            if (!_offerPauseActive)
            {
                return;
            }

            _offerPauseActive = false;
            double paused = Clock.Now - _offerPauseStartedAt;
            if (paused > 0d)
            {
                _pendingOffer.EndTime += paused;
            }
            _offerPauseStartedAt = 0d;
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

        private void BeginRunSession()
        {
            _runActive = true;
            _ordersCompleted = 0;
            _lastMusicOptionIndex = -1;
            _nextOrderId = 1;
            _nextOfferSerial = 1;
            _offerTickAccum = 0f;
            _objectiveTickAccum = 0f;
            _interactSpawnRetryAccum = 0f;
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;

            CleanupRunState();
            ClearAnchorCatalog();

            _economy.ResetSession();
            Events.Publish(new SessionBalanceChanged
            {
                Balance = 0,
                Delta = 0,
                Reason = "run_start"
            });

            EnsureRuntimeReferences();
            SpawnOffer();
        }

        private void SpawnOffer()
        {
            if (!_runActive || _pendingOffer.Active || _activeOrders.Count >= MaxActiveOrders)
            {
                return;
            }

            EnsureRuntimeReferences();
            if (_restaurantAnchors.Count <= 0 || _orderAnchors.Count <= 0)
            {
                if (!_missingAnchorLogged)
                {
                    _missingAnchorLogged = true;
                    Debug.LogWarning("[OrderFlowManager] No usable order anchors found in RunScene.");
                }
                return;
            }

            string runRegionId = ResolveCurrentRunRegionId();
            OrderFoodSelectionService.FoodSelection food =
                _foodSelectionService.PickFoodDefinition(runRegionId, DefaultDeliveryLimitSeconds);
            OrderBuildingAnchor restaurant = _anchorSelectionService.ResolveRestaurantForFood(
                food.FoodId,
                runRegionId,
                _restaurantAnchors,
                _foodRestaurantMap,
                ResolveAnchorRegion);

            OrderBuildingAnchor destination = _anchorSelectionService.ResolveRandomDestination(
                restaurant,
                runRegionId,
                _destinationAnchors,
                _orderAnchors,
                ResolveAnchorRegion);
            if (restaurant == null || destination == null)
            {
                if (!_missingAnchorLogged)
                {
                    _missingAnchorLogged = true;
                    Debug.LogWarning(
                        "[OrderFlowManager] Missing valid order anchors in selected region '" + runRegionId +
                        "'. Offer spawn deferred.");
                }

                ScheduleRespawnIfNeeded();
                return;
            }

            _missingAnchorLogged = false;
            string offerId = "A" + _nextOfferSerial;
            int orderId = _nextOrderId;
            _nextOfferSerial++;
            _nextOrderId++;

            string restaurantName = GetAnchorDisplayName(restaurant, "Restaurant");
            string destinationName = GetAnchorDisplayName(destination, "Destination");
            string pickupLabel = restaurantName + " • " + food.FoodName;
            float deliveryLimitSeconds = OrderRewardCalculator.ResolveDeliveryLimitSeconds(
                food.DeliveryLimitSeconds,
                food.IsSeafood,
                runRegionId);

            float ttl = OrderRewardCalculator.ResolveOfferTtlSeconds(BaseOfferTtlSeconds, ResolveModifierStack());
            _pendingOffer = new PendingOffer
            {
                Active = true,
                OfferId = offerId,
                OrderId = orderId,
                PickupName = pickupLabel,
                DeliveryName = destinationName,
                Reward = BaseReward,
                EndTime = Clock.Now + ttl,
                RestaurantAnchor = restaurant,
                DestinationAnchor = destination,
                FoodId = food.FoodId,
                FoodName = food.FoodName,
                FoodTempDecayMultiplier = food.TempDecayMultiplier,
                FoodSpillGainMultiplier = food.SpillGainMultiplier,
                DeliveryLimitSeconds = deliveryLimitSeconds,
                FoodIsSeafood = food.IsSeafood,
                RegionId = runRegionId
            };

            _offerTickAccum = 0f;
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;

            Events.Publish(new OfferSpawned
            {
                OfferId = _pendingOffer.OfferId,
                TtlSeconds = ttl,
                PickupName = _pendingOffer.PickupName,
                DeliveryName = _pendingOffer.DeliveryName,
                Reward = _pendingOffer.Reward,
                FoodId = _pendingOffer.FoodId,
                FoodName = _pendingOffer.FoodName
            });
        }

        private void TickPendingOffer(float unscaledDeltaTime)
        {
            if (_offerPauseActive)
            {
                return;
            }

            float remaining = (float)(_pendingOffer.EndTime - Clock.Now);
            if (remaining <= 0f)
            {
                ExpirePendingOffer();
                return;
            }

            _offerTickAccum += unscaledDeltaTime;
            if (_offerTickAccum < OfferTickInterval)
            {
                return;
            }

            _offerTickAccum = 0f;
            Events.Publish(new OfferTicked
            {
                OfferId = _pendingOffer.OfferId,
                RemainingSeconds = remaining
            });
        }
        private void ExpirePendingOffer()
        {
            if (!_pendingOffer.Active)
            {
                return;
            }

            string expiredOfferId = _pendingOffer.OfferId;
            ClearPendingOffer();
            Events.Publish(new OfferExpired { OfferId = expiredOfferId });
            ScheduleRespawnIfNeeded();
        }

        private void ScheduleRespawnIfNeeded()
        {
            if (!_runActive || !_isRunScene || _pendingRespawnHandle >= 0 || _pendingOffer.Active ||
                _activeOrders.Count >= MaxActiveOrders)
            {
                return;
            }

            _pendingRespawnHandle = Clock.Schedule(
                OrderRewardCalculator.ResolveRespawnDelaySeconds(BaseRespawnDelaySeconds, ResolveModifierStack()),
                OnRespawnDue);
        }

        private void OnRespawnDue()
        {
            _pendingRespawnHandle = -1;
            if (!_runActive || !_isRunScene || _pendingOffer.Active || _activeOrders.Count >= MaxActiveOrders)
            {
                return;
            }

            SpawnOffer();
        }

        private void CancelRespawn()
        {
            if (_pendingRespawnHandle < 0)
            {
                return;
            }

            Clock.Cancel(_pendingRespawnHandle);
            _pendingRespawnHandle = -1;
        }

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

            if (_anchorCatalogReady && _restaurantAnchors.Count > 0 && _orderAnchors.Count > 0)
            {
                return;
            }

            RebuildAnchorCatalog();
        }

        private void RebuildAnchorCatalog()
        {
            _restaurantAnchors.Clear();
            _destinationAnchors.Clear();
            _orderAnchors.Clear();
            _foodRestaurantMap.Clear();
            _anchorCatalogReady = false;

            _buildingRoleAssigner.EnsureAssigned();

            OrderBuildingAnchor[] anchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsSortMode.None);
            for (int i = 0; i < anchors.Length; i++)
            {
                OrderBuildingAnchor anchor = anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                string regionId = ResolveAnchorRegion(anchor);
                if (!IsRegionUnlockedForOrders(regionId))
                {
                    continue;
                }

                if (anchor.Role == OrderBuildingRole.GasStation)
                {
                    continue;
                }

                _orderAnchors.Add(anchor);
                if (anchor.Role == OrderBuildingRole.Restaurant)
                {
                    _restaurantAnchors.Add(anchor);
                }
                else
                {
                    _destinationAnchors.Add(anchor);
                }
            }

            if (_restaurantAnchors.Count <= 0 && _orderAnchors.Count > 0)
            {
                _restaurantAnchors.Add(_orderAnchors[0]);
            }

            if (_destinationAnchors.Count <= 0)
            {
                for (int i = 0; i < _orderAnchors.Count; i++)
                {
                    OrderBuildingAnchor anchor = _orderAnchors[i];
                    if (anchor == null)
                    {
                        continue;
                    }

                    if (anchor.Role == OrderBuildingRole.Restaurant)
                    {
                        continue;
                    }

                    _destinationAnchors.Add(anchor);
                }
            }

            if (_destinationAnchors.Count <= 0 && _orderAnchors.Count > 0)
            {
                for (int i = 0; i < _orderAnchors.Count; i++)
                {
                    _destinationAnchors.Add(_orderAnchors[i]);
                }
            }

            BuildFoodRestaurantMap();
            _anchorCatalogReady = _restaurantAnchors.Count > 0 && _orderAnchors.Count > 0;
        }

        private bool IsRegionUnlockedForOrders(string regionId)
        {
            if (_meta == null)
            {
                Services.TryGet(out _meta);
            }

            if (_meta == null)
            {
                return true;
            }

            return _meta.IsRegionUnlocked(regionId);
        }

        private static string ResolveAnchorRegion(OrderBuildingAnchor anchor)
        {
            if (anchor == null)
            {
                return "central";
            }

            string regionId = ResolveRegionIdForPosition(anchor.transform.position);
            if (anchor.RegionId != regionId)
            {
                anchor.RegionId = regionId;
            }

            return regionId;
        }

        private static string ResolveRegionIdForPosition(Vector3 worldPosition)
        {
            return RegionWorldLayout.ResolveRegionId(worldPosition);
        }

        private void BuildFoodRestaurantMap()
        {
            _foodSelectionService.BuildFoodRestaurantMap(_restaurantAnchors, _foodRestaurantMap);
        }

        private void EnsureActiveOrderInteractPoints(float unscaledDeltaTime)
        {
            if (_activeOrders.Count <= 0)
            {
                return;
            }

            _interactSpawnRetryAccum += unscaledDeltaTime;
            if (_interactSpawnRetryAccum < InteractSpawnRetryInterval)
            {
                return;
            }

            _interactSpawnRetryAccum = 0f;
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                ActiveOrder order = _activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                if (order.Stage == OrderStage.AwaitPickup)
                {
                    if (order.PickupInteract == null)
                    {
                        SpawnPickupInteractPoint(order);
                    }

                    continue;
                }

                if (order.DeliveryInteract == null)
                {
                    SpawnDeliveryInteractPoint(order);
                }
            }
        }

        private void TryHandleOrderInteractInput()
        {
            if (_activeOrders.Count <= 0)
            {
                return;
            }

            OrderInteractPoint candidate = SelectBestInteractPointCandidate();
            if (candidate == null)
            {
                return;
            }

            if (!RuntimeInput.ConsumeInteractPressedThisFrame())
            {
                return;
            }

            candidate.TryRequestInteract();
        }

        private OrderInteractPoint SelectBestInteractPointCandidate()
        {
            OrderInteractPoint bestPoint = null;
            bool bestIsDelivery = false;
            float bestSqrDistance = float.MaxValue;
            Vector3 playerPos = _player != null ? _player.transform.position : Vector3.zero;
            bool hasPlayer = _player != null;

            for (int i = 0; i < _activeOrders.Count; i++)
            {
                ActiveOrder order = _activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                bool isDelivery = order.Stage == OrderStage.Carrying;
                OrderInteractPoint point = isDelivery ? order.DeliveryInteract : order.PickupInteract;
                if (point == null || !point.IsArmed || !point.IsPlayerInRange)
                {
                    continue;
                }

                float sqrDistance = 0f;
                if (hasPlayer)
                {
                    sqrDistance = (point.transform.position - playerPos).sqrMagnitude;
                }

                if (bestPoint == null)
                {
                    bestPoint = point;
                    bestIsDelivery = isDelivery;
                    bestSqrDistance = sqrDistance;
                    continue;
                }

                if (isDelivery && !bestIsDelivery)
                {
                    bestPoint = point;
                    bestIsDelivery = true;
                    bestSqrDistance = sqrDistance;
                    continue;
                }

                if (isDelivery == bestIsDelivery && sqrDistance < bestSqrDistance)
                {
                    bestPoint = point;
                    bestSqrDistance = sqrDistance;
                }
            }

            return bestPoint;
        }

        private bool SpawnPickupInteractPoint(ActiveOrder order)
        {
            if (order == null)
            {
                return false;
            }

            DespawnPickupInteractPoint(order);
            Vector3 spawnPosition;
            if (!TryResolveRoadSidePosition(order.RestaurantAnchor, out spawnPosition))
            {
                return false;
            }

            order.PickupInteract = _interactPointFactory.Create(
                "OrderInteractPoint_Pickup_" + order.OrderId,
                order.OrderId,
                OrderPointType.Pickup,
                "P" + order.OrderId,
                order.PickupName,
                spawnPosition,
                InteractRadius,
                OnInteractPointRequested);
            return order.PickupInteract != null;
        }

        private bool SpawnDeliveryInteractPoint(ActiveOrder order)
        {
            if (order == null)
            {
                return false;
            }

            DespawnDeliveryInteractPoint(order);
            Vector3 spawnPosition;
            if (!TryResolveRoadSidePosition(order.DestinationAnchor, out spawnPosition))
            {
                return false;
            }

            order.DeliveryInteract = _interactPointFactory.Create(
                "OrderInteractPoint_Delivery_" + order.OrderId,
                order.OrderId,
                OrderPointType.Delivery,
                "D" + order.OrderId,
                order.DeliveryName,
                spawnPosition,
                InteractRadius,
                OnInteractPointRequested);
            return order.DeliveryInteract != null;
        }

        private void OnInteractPointRequested(OrderInteractPoint interactPoint)
        {
            if (interactPoint == null)
            {
                return;
            }

            Events.Publish(new OrderInteractRequested(interactPoint.OrderId, interactPoint.PointType));
        }

        private void CompleteOrderSuccess(int orderIndex, ActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            _ordersCompleted++;
            _settlementService.ProcessSuccess(
                Services,
                Events,
                Clock,
                _economy,
                _foodCfg,
                ResolveModifierStack(),
                order.OfferId,
                order.BaseReward,
                order.AcceptedAt,
                order.PickedUpAt);

            DespawnPickupInteractPoint(order);
            DespawnDeliveryInteractPoint(order);
            _activeOrders.RemoveAt(orderIndex);
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        private void PublishPrimaryObjectiveUpdate()
        {
            _objectiveSnapshots.Clear();
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                ActiveOrder order = _activeOrders[i];
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

        private string GetAnchorDisplayName(OrderBuildingAnchor anchor, string fallback)
        {
            if (anchor == null)
            {
                return fallback;
            }

            if (!string.IsNullOrEmpty(anchor.DisplayName))
            {
                return anchor.DisplayName;
            }

            return anchor.name;
        }

        private bool TryResolveRoadSidePosition(OrderBuildingAnchor anchor, out Vector3 roadSidePosition)
        {
            if (_roadQuery == null)
            {
                Services.TryGet(out _roadQuery);
            }
            return _roadSideLocator.TryResolve(anchor, _roadQuery, out roadSidePosition);
        }

        private void DespawnPickupInteractPoint(ActiveOrder order)
        {
            if (order == null || order.PickupInteract == null)
            {
                return;
            }

            order.PickupInteract.InteractRequested -= OnInteractPointRequested;
            GameObject target = order.PickupInteract.gameObject;
            order.PickupInteract = null;
            if (target != null)
            {
                Object.Destroy(target);
            }
        }

        private void DespawnDeliveryInteractPoint(ActiveOrder order)
        {
            if (order == null || order.DeliveryInteract == null)
            {
                return;
            }

            order.DeliveryInteract.InteractRequested -= OnInteractPointRequested;
            GameObject target = order.DeliveryInteract.gameObject;
            order.DeliveryInteract = null;
            if (target != null)
            {
                Object.Destroy(target);
            }
        }

        private void DespawnAllInteractPoints()
        {
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                ActiveOrder order = _activeOrders[i];
                DespawnPickupInteractPoint(order);
                DespawnDeliveryInteractPoint(order);
            }
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

        public int CopyActiveOrderTimerViewsNonAlloc(ActiveOrderTimerView[] destination)
        {
            if (destination == null || destination.Length <= 0 || _activeOrders.Count <= 0)
            {
                return 0;
            }

            int count = _activeOrders.Count < destination.Length ? _activeOrders.Count : destination.Length;
            double now = Clock.Now;

            for (int i = 0; i < count; i++)
            {
                ActiveOrder order = _activeOrders[i];
                if (order == null)
                {
                    destination[i] = default;
                    continue;
                }

                float remaining = (float)Math.Max(0d, order.DeliveryDeadlineAt - now);
                destination[i] = new ActiveOrderTimerView
                {
                    OfferId = order.OfferId,
                    FoodName = order.FoodName,
                    RemainingSeconds = remaining,
                    LimitSeconds = Mathf.Max(1f, order.DeliveryLimitSeconds),
                    IsSeafood = order.FoodIsSeafood,
                    IsCarrying = order.Stage == OrderStage.Carrying
                };
            }

            return count;
        }

        private void TickActiveOrderDeadlines()
        {
            if (_activeOrders.Count <= 0)
            {
                return;
            }

            double now = Clock.Now;
            for (int i = _activeOrders.Count - 1; i >= 0; i--)
            {
                ActiveOrder order = _activeOrders[i];
                if (order == null)
                {
                    _activeOrders.RemoveAt(i);
                    continue;
                }

                if (now < order.DeliveryDeadlineAt)
                {
                    continue;
                }

                FailOrderByTimeout(i, order);
            }
        }

        private void FailOrderByTimeout(int orderIndex, ActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            DespawnPickupInteractPoint(order);
            DespawnDeliveryInteractPoint(order);

            if (_activeOrders.Count > orderIndex)
            {
                _activeOrders.RemoveAt(orderIndex);
            }
            _settlementService.ProcessTimeout(Services, Events, order.OfferId, order.FoodName, order.DeliveryLimitSeconds);

            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
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

        private void CleanupSceneRefsAndPoints()
        {
            CancelRespawn();
            DespawnAllInteractPoints();
            PublishNoObjectives();
            _activeOrders.Clear();
            _pendingOffer = default;
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;
            _interactSpawnRetryAccum = 0f;
            _player = null;
            _missingAnchorLogged = false;
            ClearAnchorCatalog();
        }

        private void CleanupRunState()
        {
            CancelRespawn();
            DespawnAllInteractPoints();
            PublishNoObjectives();
            _activeOrders.Clear();
            _pendingOffer = default;
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;
            _offerTickAccum = 0f;
            _objectiveTickAccum = 0f;
            _interactSpawnRetryAccum = 0f;
        }

        private void ClearPendingOffer()
        {
            _pendingOffer = default;
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;
            _offerTickAccum = 0f;
        }

        private void PublishNoObjectives()
        {
            _objectiveSnapshots.Clear();
            _objectivePublisher.Publish(Events, _objectiveSnapshots, false, Vector3.zero);
        }

        private void ClearAnchorCatalog()
        {
            _restaurantAnchors.Clear();
            _destinationAnchors.Clear();
            _orderAnchors.Clear();
            _foodRestaurantMap.Clear();
            _anchorCatalogReady = false;
        }
    }
}
