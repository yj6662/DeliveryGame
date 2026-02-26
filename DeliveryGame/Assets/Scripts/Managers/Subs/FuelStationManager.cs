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
    public sealed class FuelStationManager : SubManagerBase
    {
        private const float ScenePollInterval = 0.25f;
        private const float InteractRadius = 4.5f;
        private const float SpawnYOffset = 0.3f;
        private const float FuelPerSecond = 26f;
        private const float CostPerSecond = 34f;

        private FuelService _fuel;
        private EconomyService _economy;
        private RoadQueryManager _roadQuery;

        private MotorbikeController _player;
        private OrderBuildingAnchor _stationAnchor;
        private FuelStationInteractPoint _interactPoint;

        private bool _runActive;
        private bool _isRunScene;
        private bool _isRefueling;
        private bool _missingAnchorLogged;

        private float _scenePollAccum;
        private float _costAccum;

        public override string Name => nameof(FuelStationManager);
        public override int InitOrder => 73;

        protected override void OnInitialize()
        {
            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            _isRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
            if (_isRunScene)
            {
                EnsureRuntimeReferences();
            }
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum >= ScenePollInterval)
            {
                _scenePollAccum = 0f;
                bool activeRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
                if (_isRunScene != activeRunScene)
                {
                    _isRunScene = activeRunScene;
                    if (!_isRunScene)
                    {
                        CleanupSceneObjects();
                    }
                }
            }

            if (!_runActive || !_isRunScene)
            {
                return;
            }

            EnsureRuntimeReferences();
            EnsureStationInteractPoint();

            if (!_isRefueling)
            {
                TryHandleFuelInteractInput();
            }

            if (!_isRefueling)
            {
                return;
            }

            if (_interactPoint == null || !_interactPoint.IsPlayerInRange)
            {
                StopRefueling();
                return;
            }

            if (_fuel == null)
            {
                StopRefueling();
                return;
            }

            if (_fuel.Fuel01 >= 0.999f)
            {
                StopRefueling();
                return;
            }

            if (_economy == null)
            {
                Services.TryGet(out _economy);
                if (_economy == null)
                {
                    StopRefueling();
                    return;
                }
            }

            _costAccum += CostPerSecond * unscaledDeltaTime;
            int charge = Mathf.FloorToInt(_costAccum);
            if (charge > 0)
            {
                if (!_economy.TrySpend(charge))
                {
                    StopRefueling();
                    return;
                }

                _costAccum -= charge;
                Events.Publish(new SessionBalanceChanged
                {
                    Balance = _economy.SessionBalance,
                    Delta = -charge,
                    Reason = "fuel_refill"
                });
            }

            _fuel.AddFuel(FuelPerSecond * unscaledDeltaTime);
            Events.Publish(new FuelStateChanged
            {
                Fuel01 = _fuel.Fuel01,
                CurrentFuel = _fuel.CurrentFuel,
                MaxFuel = _fuel.MaxFuel
            });
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            StopRefueling();
            CleanupSceneObjects();
            PublishRefuelState(false);
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            _costAccum = 0f;
            _missingAnchorLogged = false;
            PublishRefuelState(false);
            EnsureRuntimeReferences();
            EnsureStationInteractPoint();
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
            StopRefueling();
            CleanupSceneObjects();
            PublishRefuelState(false);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            StopRefueling();
            CleanupSceneObjects();
            PublishRefuelState(false);
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _isRunScene = evt.SceneName == SceneNames.RunScene;
            if (_isRunScene)
            {
                EnsureRuntimeReferences();
                EnsureStationInteractPoint();
            }
        }

        private void EnsureRuntimeReferences()
        {
            if (_fuel == null)
            {
                Services.TryGet(out _fuel);
            }

            if (_economy == null)
            {
                Services.TryGet(out _economy);
            }

            if (_roadQuery == null)
            {
                Services.TryGet(out _roadQuery);
            }

            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_stationAnchor != null)
            {
                return;
            }

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
                    _stationAnchor = anchor;
                    return;
                }
            }

            GameObject buildingsRootObject = GameObject.Find("CityRoot/BuildingsRoot");
            Transform buildingsRoot = buildingsRootObject != null ? buildingsRootObject.transform : null;
            if (buildingsRoot != null)
            {
                for (int i = 0; i < buildingsRoot.childCount; i++)
                {
                    Transform child = buildingsRoot.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    OrderBuildingAnchor existing = child.GetComponent<OrderBuildingAnchor>();
                    if (existing != null && existing.Role != OrderBuildingRole.GasStation)
                    {
                        continue;
                    }

                    if (existing == null)
                    {
                        existing = child.gameObject.AddComponent<OrderBuildingAnchor>();
                        existing.Role = OrderBuildingRole.GasStation;
                        existing.AnchorId = "G1";
                        existing.DisplayName = "GAS STATION";
                    }

                    _stationAnchor = existing;
                    return;
                }
            }

            if (!_missingAnchorLogged)
            {
                _missingAnchorLogged = true;
                Debug.LogWarning("[FuelStationManager] Gas station anchor not found in RunScene.");
            }
        }

        private void EnsureStationInteractPoint()
        {
            if (_interactPoint != null || _stationAnchor == null)
            {
                return;
            }

            Vector3 spawnPosition = ResolveRoadSidePosition(_stationAnchor.transform.position);
            GameObject interactObject = new GameObject("FuelStationInteractPoint");
            interactObject.transform.position = spawnPosition;
            interactObject.transform.rotation = Quaternion.identity;

            _interactPoint = interactObject.AddComponent<FuelStationInteractPoint>();
            _interactPoint.Configure(GetStationDisplayName(_stationAnchor), InteractRadius);
            _interactPoint.Arm();
            _interactPoint.InteractRequested += OnFuelInteractRequested;

            AddInteractVisual(interactObject.transform);
        }

        private static void AddInteractVisual(Transform parent)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f);

            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Color tint = new Color(1f, 0.9f, 0.2f, 1f);
                block.SetColor("_Color", tint);
                block.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(block);
            }
        }

        private void OnFuelInteractRequested(FuelStationInteractPoint point)
        {
            if (!_runActive || !_isRunScene || point == null)
            {
                return;
            }

            if (_isRefueling)
            {
                StopRefueling();
                return;
            }

            if (_economy == null)
            {
                Services.TryGet(out _economy);
            }

            if (_fuel == null)
            {
                Services.TryGet(out _fuel);
            }

            if (_economy == null || _fuel == null)
            {
                return;
            }

            if (!point.IsPlayerInRange || _economy.SessionBalance <= 0 || _fuel.Fuel01 >= 0.999f)
            {
                return;
            }

            _isRefueling = true;
            _costAccum = 0f;
            PublishRefuelState(true);
        }

        private void TryHandleFuelInteractInput()
        {
            if (_interactPoint == null || !_interactPoint.IsArmed || !_interactPoint.IsPlayerInRange)
            {
                return;
            }

            if (!RuntimeInput.ConsumeInteractPressedThisFrame())
            {
                return;
            }

            _interactPoint.TryRequestInteract();
        }

        private void StopRefueling()
        {
            if (_isRefueling)
            {
                PublishRefuelState(false);
            }

            _isRefueling = false;
            _costAccum = 0f;
        }

        private void PublishRefuelState(bool isRefueling)
        {
            Events.Publish(new FuelRefuelStateChanged
            {
                IsRefueling = isRefueling,
                CostPerSecond = CostPerSecond,
                FuelPerSecond = FuelPerSecond
            });
        }

        private void CleanupSceneObjects()
        {
            if (_interactPoint != null)
            {
                _interactPoint.InteractRequested -= OnFuelInteractRequested;
                GameObject go = _interactPoint.gameObject;
                _interactPoint = null;
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _player = null;
            _stationAnchor = null;
        }

        private Vector3 ResolveRoadSidePosition(Vector3 reference)
        {
            if (_roadQuery == null)
            {
                Services.TryGet(out _roadQuery);
            }

            Vector3 nearestRoad;
            if (_roadQuery != null && _roadQuery.GetNearestRoadPoint(reference, out nearestRoad))
            {
                nearestRoad.y += SpawnYOffset;
                return nearestRoad;
            }

            reference.y += SpawnYOffset;
            return reference;
        }

        private static string GetStationDisplayName(OrderBuildingAnchor anchor)
        {
            if (anchor == null)
            {
                return "GAS STATION";
            }

            if (!string.IsNullOrEmpty(anchor.DisplayName))
            {
                return anchor.DisplayName;
            }

            return "GAS STATION";
        }
    }
}
