using System;
using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Delivery.Input;
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

            public FoodDefinition(
                string foodId,
                string foodName,
                float tempDecayMultiplier,
                float spillGainMultiplier)
            {
                FoodId = foodId;
                FoodName = foodName;
                TempDecayMultiplier = tempDecayMultiplier;
                SpillGainMultiplier = spillGainMultiplier;
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
        }

        private const int BaseReward = 250;
        private const float BaseOfferTtlSeconds = 5f;
        private const float BaseRespawnDelaySeconds = 4.5f;
        private const float OfferTickInterval = 0.1f;
        private const float ObjectiveTickInterval = 0.25f;
        private const float ScenePollInterval = 0.25f;
        private const float InteractRadius = 4f;
        private const int MaxActiveOrders = 3;

        private static readonly FoodDefinition[] FoodDefinitions =
        {
            new FoodDefinition("burger", "Burger Set", 0.95f, 1.00f),
            new FoodDefinition("pizza", "Pizza", 1.05f, 1.08f),
            new FoodDefinition("sushi", "Sushi Box", 0.85f, 1.12f),
            new FoodDefinition("ramen", "Ramen", 1.20f, 1.18f),
            new FoodDefinition("fried_chicken", "Fried Chicken", 0.92f, 1.05f),
            new FoodDefinition("coffee", "Coffee", 1.30f, 1.25f)
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
                FoodSpillGainMultiplier = _pendingOffer.FoodSpillGainMultiplier
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
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;

            CleanupRunState();

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

            string offerId = "A" + _nextOfferSerial;
            int orderId = _nextOrderId;
            _nextOfferSerial++;
            _nextOrderId++;

            FoodDefinition food = PickFoodDefinition();
            OrderBuildingAnchor restaurant = ResolveRestaurantForFood(food.FoodId);
            OrderBuildingAnchor destination = ResolveRandomDestination(restaurant);

            string restaurantName = GetAnchorDisplayName(restaurant, "Restaurant");
            string destinationName = GetAnchorDisplayName(destination, "Destination");
            string pickupLabel = restaurantName + " • " + food.FoodName;

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
                FoodSpillGainMultiplier = food.SpillGainMultiplier
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

            int childCount = buildingsRoot.childCount;
            if (childCount <= 0)
            {
                return;
            }

            int candidateCount = 0;
            int restaurantCount = 0;
            int destinationCount = 0;
            int anchoredCount = 0;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = buildingsRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                candidateCount++;
                OrderBuildingAnchor anchor = child.GetComponent<OrderBuildingAnchor>();
                if (anchor == null)
                {
                    continue;
                }

                anchoredCount++;
                if (anchor.Role == OrderBuildingRole.Restaurant) restaurantCount++;
                else if (anchor.Role == OrderBuildingRole.Destination) destinationCount++;
            }

            if (candidateCount <= 0)
            {
                return;
            }

            if (anchoredCount == candidateCount && restaurantCount > 0 && destinationCount > 0)
            {
                return;
            }

            int gasIndex = -1;
            for (int i = 0; i < childCount; i++)
            {
                string lower = buildingsRoot.GetChild(i).name.ToLowerInvariant();
                if (lower.Contains("gas"))
                {
                    gasIndex = i;
                    break;
                }
            }

            if (gasIndex < 0)
            {
                gasIndex = childCount / 2;
            }

            int restaurantSerial = 1;
            int destinationSerial = 1;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = buildingsRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                OrderBuildingAnchor anchor = child.GetComponent<OrderBuildingAnchor>();
                if (anchor == null)
                {
                    anchor = child.gameObject.AddComponent<OrderBuildingAnchor>();
                }

                if (i == gasIndex)
                {
                    anchor.Role = OrderBuildingRole.GasStation;
                    anchor.AnchorId = "G1";
                    if (string.IsNullOrEmpty(anchor.DisplayName))
                    {
                        anchor.DisplayName = "Gas Station";
                    }
                    continue;
                }

                if ((i % 4) == 0)
                {
                    anchor.Role = OrderBuildingRole.Restaurant;
                    anchor.AnchorId = "R" + restaurantSerial;
                    if (string.IsNullOrEmpty(anchor.DisplayName))
                    {
                        anchor.DisplayName = "Restaurant " + restaurantSerial;
                    }
                    restaurantSerial++;
                }
                else
                {
                    anchor.Role = OrderBuildingRole.Destination;
                    anchor.AnchorId = "D" + destinationSerial;
                    if (string.IsNullOrEmpty(anchor.DisplayName))
                    {
                        anchor.DisplayName = "Destination " + destinationSerial;
                    }
                    destinationSerial++;
                }
            }
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

        private static FoodDefinition PickFoodDefinition()
        {
            if (FoodDefinitions.Length <= 0)
            {
                return new FoodDefinition("food", "Food", 1f, 1f);
            }

            int index = UnityEngine.Random.Range(0, FoodDefinitions.Length);
            return FoodDefinitions[index];
        }

        private OrderBuildingAnchor ResolveRestaurantForFood(string foodId)
        {
            if (!_anchorCatalogReady)
            {
                RebuildAnchorCatalog();
            }

            OrderBuildingAnchor mapped;
            if (!string.IsNullOrEmpty(foodId) && _foodRestaurantMap.TryGetValue(foodId, out mapped) && mapped != null)
            {
                return mapped;
            }

            if (_restaurantAnchors.Count > 0)
            {
                return _restaurantAnchors[0];
            }

            if (_orderAnchors.Count > 0)
            {
                return _orderAnchors[0];
            }

            return null;
        }
        private OrderBuildingAnchor ResolveRandomDestination(OrderBuildingAnchor restaurant)
        {
            OrderBuildingAnchor chosen = SelectRandomAnchor(_destinationAnchors, restaurant);
            if (chosen != null)
            {
                return chosen;
            }

            chosen = SelectRandomAnchor(_orderAnchors, restaurant);
            if (chosen != null)
            {
                return chosen;
            }

            return restaurant;
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

        private void SpawnPickupInteractPoint(ActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            DespawnPickupInteractPoint(order);
            order.PickupInteract = CreateInteractPoint(
                "OrderInteractPoint_Pickup_" + order.OrderId,
                order.OrderId,
                OrderPointType.Pickup,
                "P" + order.OrderId,
                order.PickupName,
                ResolveRoadSidePosition(order.RestaurantAnchor));
        }

        private void SpawnDeliveryInteractPoint(ActiveOrder order)
        {
            if (order == null)
            {
                return;
            }

            DespawnDeliveryInteractPoint(order);
            order.DeliveryInteract = CreateInteractPoint(
                "OrderInteractPoint_Delivery_" + order.OrderId,
                order.OrderId,
                OrderPointType.Delivery,
                "D" + order.OrderId,
                order.DeliveryName,
                ResolveRoadSidePosition(order.DestinationAnchor));
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

        private Vector3 ResolveRoadSidePosition(OrderBuildingAnchor anchor)
        {
            if (anchor == null)
            {
                return Vector3.up * 0.3f;
            }

            Vector3 reference = anchor.transform.position;
            if (_roadQuery == null)
            {
                Services.TryGet(out _roadQuery);
            }

            Vector3 nearestRoad;
            if (_roadQuery != null && _roadQuery.GetNearestRoadPoint(reference, out nearestRoad))
            {
                nearestRoad.y += 0.3f;
                return nearestRoad;
            }

            reference.y += 0.3f;
            return reference;
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
            RunReport report = new RunReport
            {
                EarnedCash = _economy.SessionBalance,
                OrdersCompleted = _ordersCompleted,
                MusicOptionIndex = _lastMusicOptionIndex
            };

            Events.Publish(new RunReportReady { Report = report });
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
