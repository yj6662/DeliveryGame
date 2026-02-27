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

        private struct FoodDefinition
        {
            public readonly string FoodId;
            public readonly string FoodName;
            public readonly float TempDecayMultiplier;
            public readonly float SpillGainMultiplier;
            public readonly float DeliveryLimitSeconds;
            public readonly bool IsSeafood;

            public FoodDefinition(
                string foodId,
                string foodName,
                float tempDecayMultiplier,
                float spillGainMultiplier,
                float deliveryLimitSeconds,
                bool isSeafood)
            {
                FoodId = foodId;
                FoodName = foodName;
                TempDecayMultiplier = tempDecayMultiplier;
                SpillGainMultiplier = spillGainMultiplier;
                DeliveryLimitSeconds = deliveryLimitSeconds;
                IsSeafood = isSeafood;
            }
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
        private const float SeasideDeliveryLimitClampMin = 45f;
        private const float GlobalDeliveryLimitClampMin = 60f;
        private const float DeliveryLimitClampMax = 240f;

        private static readonly FoodDefinition[] FoodDefinitions =
        {
            new FoodDefinition("burger", "Burger Set", 0.95f, 1.00f, 125f, false),
            new FoodDefinition("pizza", "Pizza", 1.05f, 1.08f, 130f, false),
            new FoodDefinition("ramen", "Ramen", 1.20f, 1.18f, 110f, false),
            new FoodDefinition("fried_chicken", "Fried Chicken", 0.92f, 1.05f, 122f, false),
            new FoodDefinition("coffee", "Coffee", 1.30f, 1.25f, 105f, false),
            new FoodDefinition("sushi", "Sushi Box", 0.85f, 1.12f, 88f, true),
            new FoodDefinition("shrimp_pasta", "Shrimp Pasta", 0.90f, 1.10f, 92f, true),
            new FoodDefinition("grilled_mackerel", "Grilled Mackerel", 0.88f, 1.08f, 84f, true),
            new FoodDefinition("crab_rice", "Crab Rice Bowl", 0.89f, 1.14f, 86f, true),
            new FoodDefinition("clam_chowder", "Clam Chowder", 0.94f, 1.17f, 80f, true)
        };

        private static Material s_pickupInteractMaterial;
        private static Material s_deliveryInteractMaterial;

        private readonly List<ActiveOrder> _activeOrders = new List<ActiveOrder>(8);
        private readonly List<OrderBuildingAnchor> _restaurantAnchors = new List<OrderBuildingAnchor>(32);
        private readonly List<OrderBuildingAnchor> _destinationAnchors = new List<OrderBuildingAnchor>(96);
        private readonly List<OrderBuildingAnchor> _orderAnchors = new List<OrderBuildingAnchor>(128);
        private readonly Dictionary<string, OrderBuildingAnchor> _foodRestaurantMap =
            new Dictionary<string, OrderBuildingAnchor>(16);

        private EconomyService _economy;
        private FoodStateConfigSO _foodCfg;
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
            if (evt.To == SceneNames.RunScene)
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
            FoodDefinition food = PickFoodDefinition(runRegionId);
            OrderBuildingAnchor restaurant = ResolveRestaurantForFood(food.FoodId, runRegionId);
            OrderBuildingAnchor destination = ResolveRandomDestination(restaurant, runRegionId);
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
            float deliveryLimitSeconds = ResolveDeliveryLimitSeconds(food, runRegionId);

            float ttl = ResolveOfferTtlSeconds();
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

            _pendingRespawnHandle = Clock.Schedule(ResolveRespawnDelaySeconds(), OnRespawnDue);
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

            EnsureBuildingRolesAssigned();

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

        private void EnsureBuildingRolesAssigned()
        {
            GameObject buildingsRootObject = GameObject.Find("CityRoot/BuildingsRoot");
            Transform buildingsRoot = buildingsRootObject != null ? buildingsRootObject.transform : null;
            if (buildingsRoot == null)
            {
                return;
            }

            var candidates = new List<Transform>(256);
            CollectBuildingCandidates(buildingsRoot, candidates);
            if (candidates.Count <= 0)
            {
                return;
            }

            int restaurantCount = 0;
            int destinationCount = 0;
            int anchoredCount = 0;
            var gasRegions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform child = candidates[i];
                if (child == null)
                {
                    continue;
                }

                string resolvedRegionId = ResolveRegionIdForPosition(child.position);
                OrderBuildingAnchor anchor = child.GetComponent<OrderBuildingAnchor>();
                if (anchor == null)
                {
                    continue;
                }

                if (!string.Equals(anchor.RegionId, resolvedRegionId, StringComparison.Ordinal))
                {
                    anchor.RegionId = resolvedRegionId;
                }

                anchoredCount++;
                if (anchor.Role == OrderBuildingRole.Restaurant)
                {
                    restaurantCount++;
                }
                else if (anchor.Role == OrderBuildingRole.Destination)
                {
                    destinationCount++;
                }
                else if (anchor.Role == OrderBuildingRole.GasStation)
                {
                    gasRegions.Add(resolvedRegionId);
                }
            }

            int requiredGasRegions = RegionWorldLayout.Count;
            if (anchoredCount == candidates.Count &&
                restaurantCount > 0 &&
                destinationCount > 0 &&
                gasRegions.Count >= requiredGasRegions)
            {
                return;
            }

            var gasIndexByRegion = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform child = candidates[i];
                if (child == null)
                {
                    continue;
                }

                string regionId = ResolveRegionIdForPosition(child.position);
                if (gasIndexByRegion.ContainsKey(regionId))
                {
                    continue;
                }

                string lower = child.name.ToLowerInvariant();
                if (lower.Contains("gas"))
                {
                    gasIndexByRegion[regionId] = i;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform child = candidates[i];
                if (child == null)
                {
                    continue;
                }

                string regionId = ResolveRegionIdForPosition(child.position);
                if (!gasIndexByRegion.ContainsKey(regionId))
                {
                    gasIndexByRegion[regionId] = i;
                }
            }

            int restaurantSerial = 1;
            int destinationSerial = 1;
            var regionOrderByRegion = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform child = candidates[i];
                if (child == null)
                {
                    continue;
                }

                string regionId = ResolveRegionIdForPosition(child.position);

                OrderBuildingAnchor anchor = child.GetComponent<OrderBuildingAnchor>();
                if (anchor == null)
                {
                    anchor = child.gameObject.AddComponent<OrderBuildingAnchor>();
                }

                anchor.RegionId = regionId;
                int gasIndex;
                if (gasIndexByRegion.TryGetValue(regionId, out gasIndex) && gasIndex == i)
                {
                    anchor.Role = OrderBuildingRole.GasStation;
                    anchor.AnchorId = "G_" + regionId;
                    if (string.IsNullOrEmpty(anchor.DisplayName) ||
                        !anchor.DisplayName.ToLowerInvariant().Contains("gas"))
                    {
                        anchor.DisplayName = "Gas Station (" + regionId + ")";
                    }
                    continue;
                }

                int localOrder = 0;
                if (regionOrderByRegion.TryGetValue(regionId, out localOrder))
                {
                    regionOrderByRegion[regionId] = localOrder + 1;
                }
                else
                {
                    localOrder = 0;
                    regionOrderByRegion[regionId] = 1;
                }

                if ((localOrder % 4) == 0)
                {
                    anchor.Role = OrderBuildingRole.Restaurant;
                    anchor.AnchorId = "R" + restaurantSerial;
                    if (string.IsNullOrEmpty(anchor.DisplayName))
                    {
                        anchor.DisplayName = "Restaurant " + restaurantSerial + " (" + regionId + ")";
                    }
                    restaurantSerial++;
                }
                else
                {
                    anchor.Role = OrderBuildingRole.Destination;
                    anchor.AnchorId = "D" + destinationSerial;
                    if (string.IsNullOrEmpty(anchor.DisplayName))
                    {
                        anchor.DisplayName = "Destination " + destinationSerial + " (" + regionId + ")";
                    }
                    destinationSerial++;
                }
            }
        }

        private static void CollectBuildingCandidates(Transform buildingsRoot, List<Transform> destination)
        {
            if (buildingsRoot == null || destination == null)
            {
                return;
            }

            destination.Clear();

            for (int i = 0; i < buildingsRoot.childCount; i++)
            {
                Transform direct = buildingsRoot.GetChild(i);
                if (direct == null)
                {
                    continue;
                }

                bool collectedFromChildren = false;
                if (direct.childCount > 0 && direct.GetComponent<Renderer>() == null)
                {
                    for (int j = 0; j < direct.childCount; j++)
                    {
                        Transform nested = direct.GetChild(j);
                        if (nested == null)
                        {
                            continue;
                        }

                        if (nested.GetComponentInChildren<Renderer>(true) == null)
                        {
                            continue;
                        }

                        destination.Add(nested);
                        collectedFromChildren = true;
                    }
                }

                if (collectedFromChildren)
                {
                    continue;
                }

                if (direct.GetComponentInChildren<Renderer>(true) != null)
                {
                    destination.Add(direct);
                }
            }
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
            _foodRestaurantMap.Clear();
            if (_restaurantAnchors.Count <= 0)
            {
                return;
            }

            int restCount = _restaurantAnchors.Count;
            for (int i = 0; i < FoodDefinitions.Length; i++)
            {
                FoodDefinition food = FoodDefinitions[i];
                OrderBuildingAnchor anchor = _restaurantAnchors[i % restCount];
                _foodRestaurantMap[food.FoodId] = anchor;
            }
        }

        private static FoodDefinition PickFoodDefinition(string regionId)
        {
            if (FoodDefinitions.Length <= 0)
            {
                return new FoodDefinition("food", "Food", 1f, 1f, DefaultDeliveryLimitSeconds, false);
            }

            bool seasideRegion = string.Equals(regionId, "seaside", StringComparison.Ordinal);
            int seafoodCount = 0;
            int nonSeafoodCount = 0;
            for (int i = 0; i < FoodDefinitions.Length; i++)
            {
                if (FoodDefinitions[i].IsSeafood)
                {
                    seafoodCount++;
                }
                else
                {
                    nonSeafoodCount++;
                }
            }

            if (seasideRegion && seafoodCount > 0)
            {
                int seafoodWeight = 85;
                int roll = UnityEngine.Random.Range(0, 100);
                bool pickSeafood = roll < seafoodWeight || nonSeafoodCount <= 0;
                if (pickSeafood)
                {
                    int seafoodIndex = UnityEngine.Random.Range(0, seafoodCount);
                    int seen = 0;
                    for (int i = 0; i < FoodDefinitions.Length; i++)
                    {
                        if (!FoodDefinitions[i].IsSeafood)
                        {
                            continue;
                        }

                        if (seen == seafoodIndex)
                        {
                            return FoodDefinitions[i];
                        }

                        seen++;
                    }
                }
            }

            int index = UnityEngine.Random.Range(0, FoodDefinitions.Length);
            return FoodDefinitions[index];
        }

        private OrderBuildingAnchor ResolveRestaurantForFood(string foodId, string preferredRegionId)
        {
            if (string.IsNullOrEmpty(preferredRegionId))
            {
                preferredRegionId = ResolveCurrentRunRegionId();
            }

            if (!_anchorCatalogReady)
            {
                RebuildAnchorCatalog();
            }

            OrderBuildingAnchor mapped = null;
            if (!string.IsNullOrEmpty(foodId) && _foodRestaurantMap.TryGetValue(foodId, out mapped) && mapped != null)
            {
                if (string.Equals(ResolveAnchorRegion(mapped), preferredRegionId, StringComparison.Ordinal))
                {
                    return mapped;
                }
            }

            OrderBuildingAnchor regional = SelectRandomAnchorInRegion(_restaurantAnchors, null, preferredRegionId);
            if (regional != null)
            {
                return regional;
            }

            return null;
        }
        private OrderBuildingAnchor ResolveRandomDestination(OrderBuildingAnchor restaurant, string preferredRegionId)
        {
            if (string.IsNullOrEmpty(preferredRegionId))
            {
                preferredRegionId = ResolveCurrentRunRegionId();
            }

            OrderBuildingAnchor regional = SelectRandomAnchorInRegion(_destinationAnchors, restaurant, preferredRegionId);
            if (regional != null)
            {
                return regional;
            }

            return SelectRandomAnchorInRegion(_orderAnchors, restaurant, preferredRegionId);
        }

        private static OrderBuildingAnchor SelectRandomAnchorInRegion(
            List<OrderBuildingAnchor> anchors,
            OrderBuildingAnchor excluded,
            string regionId)
        {
            if (anchors == null || anchors.Count <= 0 || string.IsNullOrEmpty(regionId))
            {
                return null;
            }

            OrderBuildingAnchor selected = null;
            int selectable = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                OrderBuildingAnchor candidate = anchors[i];
                if (candidate == null || candidate == excluded)
                {
                    continue;
                }

                string candidateRegion = ResolveAnchorRegion(candidate);
                if (!string.Equals(candidateRegion, regionId, StringComparison.Ordinal))
                {
                    continue;
                }

                selectable++;
                if (UnityEngine.Random.Range(0, selectable) == 0)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private static OrderBuildingAnchor SelectRandomAnchor(List<OrderBuildingAnchor> anchors, OrderBuildingAnchor excluded)
        {
            if (anchors == null || anchors.Count <= 0)
            {
                return null;
            }

            OrderBuildingAnchor selected = null;
            int selectable = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                OrderBuildingAnchor candidate = anchors[i];
                if (candidate == null || candidate == excluded)
                {
                    continue;
                }

                selectable++;
                if (UnityEngine.Random.Range(0, selectable) == 0)
                {
                    selected = candidate;
                }
            }

            return selected;
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

            order.PickupInteract = CreateInteractPoint(
                "OrderInteractPoint_Pickup_" + order.OrderId,
                order.OrderId,
                OrderPointType.Pickup,
                "P" + order.OrderId,
                order.PickupName,
                spawnPosition);
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

            order.DeliveryInteract = CreateInteractPoint(
                "OrderInteractPoint_Delivery_" + order.OrderId,
                order.OrderId,
                OrderPointType.Delivery,
                "D" + order.OrderId,
                order.DeliveryName,
                spawnPosition);
            return order.DeliveryInteract != null;
        }

        private OrderInteractPoint CreateInteractPoint(
            string objectName,
            int orderId,
            OrderPointType pointType,
            string pointId,
            string displayName,
            Vector3 spawnPos)
        {
            GameObject interactObject = new GameObject(objectName);
            interactObject.transform.position = spawnPos;
            interactObject.transform.rotation = Quaternion.identity;

            OrderInteractPoint interactPoint = interactObject.AddComponent<OrderInteractPoint>();
            interactPoint.Configure(pointType, pointId, displayName);
            interactPoint.SetInteractRadius(InteractRadius);
            interactPoint.Arm(orderId, pointType);
            interactPoint.InteractRequested += OnInteractPointRequested;

            AddInteractVisual(interactObject.transform, pointType);
            return interactPoint;
        }

        private static void AddInteractVisual(Transform parent, OrderPointType pointType)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            visual.transform.localScale = new Vector3(1.4f, 0.2f, 1.4f);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Object.Destroy(visualCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color tint = pointType == OrderPointType.Pickup
                    ? new Color(0.2f, 0.9f, 0.3f, 1f)
                    : new Color(0.2f, 0.45f, 1f, 1f);
                renderer.sharedMaterial = GetInteractVisualMaterial(pointType, tint);
            }
        }

        private static Material GetInteractVisualMaterial(OrderPointType pointType, Color tint)
        {
            Material cached = pointType == OrderPointType.Pickup
                ? s_pickupInteractMaterial
                : s_deliveryInteractMaterial;
            if (cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = pointType == OrderPointType.Pickup
                ? "OrderInteract_Pickup_Opaque"
                : "OrderInteract_Delivery_Opaque";
            material.SetColor("_Color", tint);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (pointType == OrderPointType.Pickup)
            {
                s_pickupInteractMaterial = material;
            }
            else
            {
                s_deliveryInteractMaterial = material;
            }

            return material;
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

            float qualityMul = 1f;
            float quality = 1f;
            float temperature = 1f;
            float spill = 0f;

            FoodStateService food;
            if (Services.TryGet(out food) && food != null && food.IsActive &&
                string.Equals(food.ActiveOfferId, order.OfferId, StringComparison.Ordinal))
            {
                quality = food.ComputeQuality01();
                temperature = food.Temperature01;
                spill = food.Spill01;
                if (_foodCfg != null)
                {
                    qualityMul = food.ComputeRewardMultiplier(quality, _foodCfg);
                }

                food.Reset();
            }

            float musicMul = 1f;
            if (_stack == null)
            {
                Services.TryGet(out _stack);
            }
            if (_stack != null)
            {
                musicMul = _stack.GetMul(RunStatId.RewardMultiplier);
            }

            float elapsedFromAccept = 0f;
            if (order.AcceptedAt >= 0d)
            {
                elapsedFromAccept = (float)Math.Max(0d, Clock.Now - order.AcceptedAt);
            }

            float elapsedFromPickup = 0f;
            if (order.PickedUpAt >= 0d)
            {
                elapsedFromPickup = (float)Math.Max(0d, Clock.Now - order.PickedUpAt);
            }

            float timeMul = EvaluateDeliveryTimeMultiplier(elapsedFromAccept);
            int finalReward = Mathf.Max(0, Mathf.RoundToInt(order.BaseReward * musicMul * qualityMul * timeMul));

            Events.Publish(new FoodQualityComputed
            {
                OfferId = order.OfferId,
                Temperature01 = temperature,
                Spill01 = spill,
                Quality01 = quality,
                RewardMultiplier = qualityMul,
                RatingDelta = 0f
            });

            _economy.AddReward(finalReward, "order_complete");
            Events.Publish(new SessionBalanceChanged
            {
                Balance = _economy.SessionBalance,
                Delta = finalReward,
                Reason = "order_complete"
            });

            Events.Publish(new OrderDeliveryReached
            {
                OfferId = order.OfferId,
                Reward = finalReward
            });

            Events.Publish(new OrderCompleted
            {
                OfferId = order.OfferId,
                Reward = finalReward
            });

            Events.Publish(new OrderObjectiveUpdated
            {
                OfferId = order.OfferId,
                Text = order.OfferId + " COMPLETE +$" + finalReward + " (" + elapsedFromAccept.ToString("0.0") + "s / " +
                       elapsedFromPickup.ToString("0.0") + "s)",
                DistanceMeters = 0f
            });

            DespawnPickupInteractPoint(order);
            DespawnDeliveryInteractPoint(order);
            _activeOrders.RemoveAt(orderIndex);
            ScheduleRespawnIfNeeded();
            PublishPrimaryObjectiveUpdate();
        }

        private void PublishPrimaryObjectiveUpdate()
        {
            ActiveOrder primary = GetPrimaryOrder();
            if (primary == null)
            {
                PublishObjectiveMarkerInactive();
                PublishObjectiveMarkersSnapshot();
                return;
            }

            OrderInteractPoint interactPoint = primary.Stage == OrderStage.AwaitPickup
                ? primary.PickupInteract
                : primary.DeliveryInteract;

            string prefix = primary.Stage == OrderStage.AwaitPickup ? "GO PICKUP: " : "DELIVER TO: ";
            OrderPointType pointType = primary.Stage == OrderStage.AwaitPickup ? OrderPointType.Pickup : OrderPointType.Delivery;
            float distance = ComputeDistance(interactPoint);
            string text = prefix + (primary.Stage == OrderStage.AwaitPickup ? primary.PickupName : primary.DeliveryName);
            if (distance >= 0f)
            {
                text += " (" + Mathf.RoundToInt(distance) + "m)";
            }

            Events.Publish(new OrderObjectiveUpdated
            {
                OfferId = primary.OfferId,
                Text = text,
                DistanceMeters = distance
            });

            PublishObjectiveMarker(primary.OfferId, interactPoint, pointType);
            PublishObjectiveMarkersSnapshot();
        }
        private ActiveOrder GetPrimaryOrder()
        {
            if (_activeOrders.Count <= 0)
            {
                return null;
            }

            for (int i = 0; i < _activeOrders.Count; i++)
            {
                if (_activeOrders[i].Stage == OrderStage.Carrying)
                {
                    return _activeOrders[i];
                }
            }

            return _activeOrders[0];
        }

        private float ComputeDistance(OrderInteractPoint interactPoint)
        {
            if (_player == null || interactPoint == null)
            {
                return -1f;
            }

            return Vector3.Distance(_player.transform.position, interactPoint.transform.position);
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
            roadSidePosition = Vector3.zero;
            if (anchor == null)
            {
                return false;
            }

            Vector3 reference = anchor.transform.position;
            if (_roadQuery == null)
            {
                Services.TryGet(out _roadQuery);
            }

            Vector3 nearestRoad;
            if (_roadQuery != null)
            {
                _roadQuery.RefreshRoadCache();
                if (_roadQuery.GetNearestRoadPoint(reference, out nearestRoad))
                {
                    nearestRoad.y += 0.3f;
                    roadSidePosition = nearestRoad;
                    return true;
                }
            }

            RoadSurface[] surfaces = Object.FindObjectsByType<RoadSurface>(FindObjectsSortMode.None);
            float bestSqrDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < surfaces.Length; i++)
            {
                RoadSurface surface = surfaces[i];
                if (surface == null)
                {
                    continue;
                }

                Collider collider = surface.CachedCollider;
                if (collider == null)
                {
                    collider = surface.GetComponent<Collider>();
                }

                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 candidate = collider.ClosestPoint(reference);
                float sqrDistance = (candidate - reference).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                roadSidePosition = candidate;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            roadSidePosition.y += 0.3f;
            return true;
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

        private void PublishObjectiveMarker(string offerId, OrderInteractPoint point, OrderPointType pointType)
        {
            if (point == null)
            {
                PublishObjectiveMarkerInactive();
                return;
            }

            Events.Publish(new OrderObjectiveMarkerUpdated
            {
                OfferId = offerId,
                Active = true,
                PointType = pointType,
                WorldPosition = point.transform.position
            });
        }

        private void PublishObjectiveMarkerInactive()
        {
            Events.Publish(new OrderObjectiveMarkerUpdated
            {
                OfferId = string.Empty,
                Active = false,
                PointType = OrderPointType.Pickup,
                WorldPosition = Vector3.zero
            });
        }

        private void PublishObjectiveMarkersSnapshot()
        {
            OrderObjectiveMarkersUpdated markers = new OrderObjectiveMarkersUpdated
            {
                Count = 0,
                OfferId0 = string.Empty,
                OfferId1 = string.Empty,
                OfferId2 = string.Empty
            };

            int count = 0;
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                if (count >= 3)
                {
                    break;
                }

                ActiveOrder order = _activeOrders[i];
                if (order == null)
                {
                    continue;
                }

                OrderInteractPoint interactPoint = order.Stage == OrderStage.AwaitPickup
                    ? order.PickupInteract
                    : order.DeliveryInteract;
                if (interactPoint == null)
                {
                    continue;
                }

                OrderPointType pointType = order.Stage == OrderStage.AwaitPickup
                    ? OrderPointType.Pickup
                    : OrderPointType.Delivery;

                if (count == 0)
                {
                    markers.OfferId0 = order.OfferId;
                    markers.PointType0 = pointType;
                    markers.WorldPosition0 = interactPoint.transform.position;
                }
                else if (count == 1)
                {
                    markers.OfferId1 = order.OfferId;
                    markers.PointType1 = pointType;
                    markers.WorldPosition1 = interactPoint.transform.position;
                }
                else
                {
                    markers.OfferId2 = order.OfferId;
                    markers.PointType2 = pointType;
                    markers.WorldPosition2 = interactPoint.transform.position;
                }

                count++;
            }

            markers.Count = count;
            Events.Publish(markers);
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

            FoodStateService food;
            if (Services.TryGet(out food) && food != null && food.IsActive &&
                string.Equals(food.ActiveOfferId, order.OfferId, StringComparison.Ordinal))
            {
                food.Reset();
            }

            Events.Publish(new OrderTimedOut
            {
                OfferId = order.OfferId,
                FoodName = order.FoodName,
                LimitSeconds = Mathf.Max(1f, order.DeliveryLimitSeconds)
            });

            Events.Publish(new OrderObjectiveUpdated
            {
                OfferId = order.OfferId,
                Text = order.OfferId + " FAILED (TIME OUT)",
                DistanceMeters = 0f
            });

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

        private static float ResolveDeliveryLimitSeconds(FoodDefinition food, string runRegionId)
        {
            float limit = food.DeliveryLimitSeconds > 0.1f ? food.DeliveryLimitSeconds : DefaultDeliveryLimitSeconds;
            bool seasideRegion = string.Equals(runRegionId, "seaside", StringComparison.Ordinal);

            if (seasideRegion)
            {
                if (food.IsSeafood)
                {
                    limit *= 0.70f;
                }
                else
                {
                    limit *= 0.82f;
                }

                return Mathf.Clamp(limit, SeasideDeliveryLimitClampMin, DeliveryLimitClampMax);
            }

            if (food.IsSeafood)
            {
                limit *= 0.88f;
            }

            return Mathf.Clamp(limit, GlobalDeliveryLimitClampMin, DeliveryLimitClampMax);
        }

        private float ResolveOfferTtlSeconds()
        {
            float ttl = BaseOfferTtlSeconds;
            if (_stack == null)
            {
                Services.TryGet(out _stack);
            }

            if (_stack != null)
            {
                ttl *= _stack.GetMul(RunStatId.OfferAcceptTtlMultiplier);
            }

            return Mathf.Clamp(ttl, 1.5f, 30f);
        }

        private float ResolveRespawnDelaySeconds()
        {
            float delay = BaseRespawnDelaySeconds;
            if (_stack == null)
            {
                Services.TryGet(out _stack);
            }

            if (_stack != null)
            {
                delay *= _stack.GetMul(RunStatId.OfferRespawnDelayMultiplier);
            }

            return Mathf.Clamp(delay, 0.25f, 15f);
        }

        private static float EvaluateDeliveryTimeMultiplier(float elapsedSeconds)
        {
            if (elapsedSeconds <= 30f) return 1.30f;
            if (elapsedSeconds <= 50f) return 1.15f;
            if (elapsedSeconds <= 75f) return 1.00f;
            if (elapsedSeconds <= 105f) return 0.90f;
            return 0.80f;
        }

        private void HandleSceneChanged(string sceneName)
        {
            bool isRun = sceneName == SceneNames.RunScene;
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
            PublishObjectiveMarkerInactive();
            PublishObjectiveMarkersSnapshot();
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
            PublishObjectiveMarkerInactive();
            PublishObjectiveMarkersSnapshot();
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
