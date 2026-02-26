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
        private enum InternalState
        {
            Idle = 0,
            Offering = 1,
            Accepted = 2,
            PickedUp = 3,
            Completed = 4
        }

        private const string FixedOfferId = "A1";
        private const int FixedOrderId = 1;
        private const int FixedReward = 250;
        private const float OfferTtlSeconds = 5f;
        private const float RespawnDelaySeconds = 2f;
        private const float OfferTickInterval = 0.1f;
        private const float ObjectiveTickInterval = 0.25f;
        private const float ScenePollInterval = 0.25f;
        private const float InteractRadius = 4f;

        private EconomyService _economy;
        private FoodStateConfigSO _foodCfg;
        private MotorbikeController _player;
        private RoadQueryManager _roadQuery;
        private OrderBuildingAnchor _restaurantAnchor;
        private OrderBuildingAnchor _destinationAnchor;
        private OrderInteractPoint _pickupInteract;
        private OrderInteractPoint _deliveryInteract;

        private InternalState _state;
        private bool _runActive;
        private bool _isRunScene;
        private bool _missingAnchorLogged;

        private double _offerEndTime;
        private float _offerTickAccum;
        private float _objectiveTickAccum;
        private float _scenePollAccum;

        private int _pendingRespawnHandle = -1;
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

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<AcceptOfferRequested>(Events, OnAcceptRequested);
            Subs.Add<OrderInteractRequested>(Events, OnOrderInteractRequested);
            Subs.Add<MusicChoiceSelected>(Events, OnMusicChoiceSelected);
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
            if (_state == InternalState.Offering)
            {
                TickOffer(unscaledDeltaTime);
                return;
            }

            if (_state == InternalState.Accepted || _state == InternalState.PickedUp)
            {
                _objectiveTickAccum += unscaledDeltaTime;
                if (_objectiveTickAccum >= ObjectiveTickInterval)
                {
                    _objectiveTickAccum = 0f;
                    PublishObjectiveUpdate();
                }
            }
        }

        protected override void OnShutdown()
        {
            CancelRespawn();
            DespawnAllInteractPoints();
            PublishObjectiveMarkerInactive();
            _state = InternalState.Idle;
            _runActive = false;
            _isRunScene = false;
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

            CancelRespawn();
            DespawnAllInteractPoints();
            PublishObjectiveMarkerInactive();
            _runActive = false;
            _state = InternalState.Idle;
            _offerTickAccum = 0f;
            _objectiveTickAccum = 0f;
        }

        private void OnAcceptRequested(AcceptOfferRequested evt)
        {
            if (!_runActive || _state != InternalState.Offering)
            {
                return;
            }

            if (!string.Equals(evt.OfferId, FixedOfferId, System.StringComparison.Ordinal))
            {
                return;
            }

            if (Clock.Now > _offerEndTime)
            {
                ExpireOffer();
                return;
            }

            _state = InternalState.Accepted;
            _objectiveTickAccum = 0f;
            Events.Publish(new OfferAccepted { OfferId = FixedOfferId });

            SpawnPickupInteractPoint();
            PublishCurrentObjectiveMarker();
            PublishObjectiveUpdate();
        }

        private void OnOrderInteractRequested(OrderInteractRequested evt)
        {
            if (!_runActive || evt.OrderId != FixedOrderId)
            {
                return;
            }

            if (_state == InternalState.Accepted && evt.PointType == OrderPointType.Pickup)
            {
                _state = InternalState.PickedUp;
                _objectiveTickAccum = 0f;
                DespawnPickupInteractPoint();
                Events.Publish(new OrderPickupReached { OfferId = FixedOfferId });
                SpawnDeliveryInteractPoint();
                PublishCurrentObjectiveMarker();
                PublishObjectiveUpdate();
                return;
            }

            if (_state == InternalState.PickedUp && evt.PointType == OrderPointType.Delivery)
            {
                CompleteOrderSuccess();
            }
        }

        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            _lastMusicOptionIndex = evt.OptionIndex;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            DespawnAllInteractPoints();
            PublishObjectiveMarkerInactive();
            _player = null;
            _restaurantAnchor = null;
            _destinationAnchor = null;
            _missingAnchorLogged = false;
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName);
        }

        private void BeginRunSession()
        {
            _runActive = true;
            _state = InternalState.Offering;
            _offerTickAccum = 0f;
            _objectiveTickAccum = 0f;
            _ordersCompleted = 0;
            _lastMusicOptionIndex = -1;
            _missingAnchorLogged = false;

            CancelRespawn();
            DespawnAllInteractPoints();
            PublishObjectiveMarkerInactive();

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
            if (!_runActive)
            {
                return;
            }

            _state = InternalState.Offering;
            _offerEndTime = Clock.Now + OfferTtlSeconds;
            _offerTickAccum = 0f;
            PublishObjectiveMarkerInactive();

            string pickupName = GetAnchorDisplayName(_restaurantAnchor, "PICKUP: Restaurant");
            string deliveryName = GetAnchorDisplayName(_destinationAnchor, "DELIVER: Destination");

            Events.Publish(new OfferSpawned
            {
                OfferId = FixedOfferId,
                TtlSeconds = OfferTtlSeconds,
                PickupName = pickupName,
                DeliveryName = deliveryName,
                Reward = FixedReward
            });
        }

        private void TickOffer(float unscaledDeltaTime)
        {
            float remaining = (float)(_offerEndTime - Clock.Now);
            if (remaining <= 0f)
            {
                ExpireOffer();
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
                OfferId = FixedOfferId,
                RemainingSeconds = remaining
            });
        }

        private void ExpireOffer()
        {
            if (_state != InternalState.Offering)
            {
                return;
            }

            _state = InternalState.Idle;
            DespawnAllInteractPoints();
            PublishObjectiveMarkerInactive();
            Events.Publish(new OfferExpired { OfferId = FixedOfferId });
            ScheduleRespawnIfNeeded();
        }

        private void ScheduleRespawnIfNeeded()
        {
            if (!_runActive || _state == InternalState.Completed || _pendingRespawnHandle >= 0)
            {
                return;
            }

            _pendingRespawnHandle = Clock.Schedule(RespawnDelaySeconds, OnRespawnDue);
        }

        private void OnRespawnDue()
        {
            _pendingRespawnHandle = -1;
            if (!_runActive || !_isRunScene || _state == InternalState.Completed)
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

            if (_restaurantAnchor != null && _destinationAnchor != null)
            {
                return;
            }

            _restaurantAnchor = null;
            _destinationAnchor = null;

            OrderBuildingAnchor[] anchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsSortMode.None);
            for (int i = 0; i < anchors.Length; i++)
            {
                OrderBuildingAnchor anchor = anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                if (anchor.Role == OrderBuildingRole.Restaurant && _restaurantAnchor == null)
                {
                    _restaurantAnchor = anchor;
                    continue;
                }

                if (anchor.Role == OrderBuildingRole.Destination && _destinationAnchor == null)
                {
                    _destinationAnchor = anchor;
                }
            }

            if ((_restaurantAnchor == null || _destinationAnchor == null) && !_missingAnchorLogged)
            {
                _missingAnchorLogged = true;
                Debug.LogWarning("[OrderFlowManager] Missing OrderBuildingAnchor(s): Restaurant/Destination.");
            }
        }

        private void SpawnPickupInteractPoint()
        {
            DespawnPickupInteractPoint();
            EnsureRuntimeReferences();

            _pickupInteract = CreateInteractPoint(
                "OrderInteractPoint_Pickup",
                OrderPointType.Pickup,
                "P1",
                GetAnchorDisplayName(_restaurantAnchor, "PICKUP"));
        }

        private void SpawnDeliveryInteractPoint()
        {
            DespawnDeliveryInteractPoint();
            EnsureRuntimeReferences();

            _deliveryInteract = CreateInteractPoint(
                "OrderInteractPoint_Delivery",
                OrderPointType.Delivery,
                "D1",
                GetAnchorDisplayName(_destinationAnchor, "DELIVERY"));
        }

        private OrderInteractPoint CreateInteractPoint(string objectName, OrderPointType pointType, string pointId, string displayName)
        {
            Vector3 spawnPos = pointType == OrderPointType.Pickup
                ? ResolveRoadSidePosition(_restaurantAnchor)
                : ResolveRoadSidePosition(_destinationAnchor);

            GameObject interactObject = new GameObject(objectName);
            interactObject.transform.position = spawnPos;
            interactObject.transform.rotation = Quaternion.identity;

            OrderInteractPoint interactPoint = interactObject.AddComponent<OrderInteractPoint>();
            interactPoint.Configure(pointType, pointId, displayName);
            interactPoint.SetInteractRadius(InteractRadius);
            interactPoint.Arm(FixedOrderId, pointType);
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

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", tint);
                block.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(block);
            }
        }

        private void OnInteractPointRequested(OrderInteractPoint interactPoint)
        {
            if (interactPoint == null)
            {
                return;
            }

            Events.Publish(new OrderInteractRequested(interactPoint.OrderId, interactPoint.PointType));
        }

        private void CompleteOrderSuccess()
        {
            _state = InternalState.Completed;
            _ordersCompleted++;
            DespawnDeliveryInteractPoint();
            PublishObjectiveMarkerInactive();

            float qualityMul = 1f;
            float quality = 1f;
            float temperature = 1f;
            float spill = 0f;

            FoodStateService food;
            if (Services.TryGet(out food) && food != null && food.IsActive &&
                string.Equals(food.ActiveOfferId, FixedOfferId, System.StringComparison.Ordinal))
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
            ModifierStackService stack;
            if (Services.TryGet(out stack) && stack != null)
            {
                musicMul = stack.GetMul(RunStatId.RewardMultiplier);
            }

            int finalReward = Mathf.Max(0, Mathf.RoundToInt(FixedReward * musicMul * qualityMul));

            Events.Publish(new FoodQualityComputed
            {
                OfferId = FixedOfferId,
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
                OfferId = FixedOfferId,
                Reward = finalReward
            });

            Events.Publish(new OrderCompleted
            {
                OfferId = FixedOfferId,
                Reward = finalReward
            });

            Events.Publish(new OrderObjectiveUpdated
            {
                Text = "ORDER COMPLETE! +$" + finalReward,
                DistanceMeters = 0f
            });
        }

        private void PublishObjectiveUpdate()
        {
            string text;
            float distance;
            if (_state == InternalState.Accepted)
            {
                text = BuildObjectiveText("GO PICKUP: ", _pickupInteract, _restaurantAnchor);
                distance = ComputeDistance(_pickupInteract);
            }
            else if (_state == InternalState.PickedUp)
            {
                text = BuildObjectiveText("DELIVER TO: ", _deliveryInteract, _destinationAnchor);
                distance = ComputeDistance(_deliveryInteract);
            }
            else
            {
                return;
            }

            Events.Publish(new OrderObjectiveUpdated
            {
                Text = text,
                DistanceMeters = distance
            });
        }

        private string BuildObjectiveText(string prefix, OrderInteractPoint interactPoint, OrderBuildingAnchor anchor)
        {
            if (interactPoint == null)
            {
                return prefix + "UNKNOWN";
            }

            float distance = ComputeDistance(interactPoint);
            string targetName = GetAnchorDisplayName(anchor, interactPoint.DisplayName);
            if (distance < 0f)
            {
                return prefix + targetName;
            }

            return prefix + targetName + " (" + Mathf.RoundToInt(distance) + "m)";
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

        private void DespawnAllInteractPoints()
        {
            DespawnPickupInteractPoint();
            DespawnDeliveryInteractPoint();
        }

        private void DespawnPickupInteractPoint()
        {
            if (_pickupInteract == null)
            {
                return;
            }

            _pickupInteract.InteractRequested -= OnInteractPointRequested;
            GameObject target = _pickupInteract.gameObject;
            _pickupInteract = null;
            if (target != null)
            {
                Object.Destroy(target);
            }
        }

        private void DespawnDeliveryInteractPoint()
        {
            if (_deliveryInteract == null)
            {
                return;
            }

            _deliveryInteract.InteractRequested -= OnInteractPointRequested;
            GameObject target = _deliveryInteract.gameObject;
            _deliveryInteract = null;
            if (target != null)
            {
                Object.Destroy(target);
            }
        }

        private void PublishCurrentObjectiveMarker()
        {
            if (_state == InternalState.Accepted)
            {
                PublishObjectiveMarker(_pickupInteract, OrderPointType.Pickup);
                return;
            }

            if (_state == InternalState.PickedUp)
            {
                PublishObjectiveMarker(_deliveryInteract, OrderPointType.Delivery);
                return;
            }

            PublishObjectiveMarkerInactive();
        }

        private void PublishObjectiveMarker(OrderInteractPoint point, OrderPointType pointType)
        {
            if (point == null)
            {
                PublishObjectiveMarkerInactive();
                return;
            }

            Events.Publish(new OrderObjectiveMarkerUpdated
            {
                OfferId = FixedOfferId,
                Active = true,
                PointType = pointType,
                WorldPosition = point.transform.position
            });
        }

        private void PublishObjectiveMarkerInactive()
        {
            Events.Publish(new OrderObjectiveMarkerUpdated
            {
                OfferId = FixedOfferId,
                Active = false,
                PointType = OrderPointType.Pickup,
                WorldPosition = Vector3.zero
            });
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
                DespawnAllInteractPoints();
                PublishObjectiveMarkerInactive();
                _player = null;
                _restaurantAnchor = null;
                _destinationAnchor = null;
                _missingAnchorLogged = false;
                return;
            }

            EnsureRuntimeReferences();
        }
    }
}
