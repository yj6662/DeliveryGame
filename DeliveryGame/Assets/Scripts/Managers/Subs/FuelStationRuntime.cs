using DeliveryRun;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class FuelStationRuntime
    {
        private const float InteractRadius = 4.5f;
        private const float FuelPerSecond = 26f;
        private const float CostPerSecond = 34f;

        private readonly FuelService _fuel;
        private readonly ServiceRegistry _services;
        private readonly EventBus _events;
        private readonly FuelStationAnchorResolver _anchorResolver;

        private EconomyService _economy;
        private FuelStationInteractPoint _interactPoint;
        private bool _isRefueling;
        private float _costAccum;

        internal FuelStationRuntime(FuelService fuel, ServiceRegistry services, EventBus events)
        {
            _fuel = fuel;
            _services = services;
            _events = events;
            _anchorResolver = new FuelStationAnchorResolver(services);
        }

        internal void OnRunStarted(bool isRunScene)
        {
            _costAccum = 0f;
            _anchorResolver.ResetRegionTracking();
            PublishRefuelState(false);

            if (isRunScene)
            {
                _anchorResolver.EnsureRuntimeReferences();
            }
        }

        internal void OnRunEnded()
        {
            StopRefueling();
            CleanupSceneObjects();
            PublishRefuelState(false);
        }

        internal void OnShutdown()
        {
            StopRefueling();
            CleanupSceneObjects();
            PublishRefuelState(false);
        }

        internal void OnExitRunScene()
        {
            StopRefueling();
            CleanupSceneObjects();
            PublishRefuelState(false);
        }

        internal void OnEnterRunScene()
        {
            _anchorResolver.EnsureRuntimeReferences();
            bool anchorChanged = _anchorResolver.UpdateStationAnchorForCurrentRegion(true);
            if (anchorChanged)
            {
                StopRefueling();
                ClearInteractPoint();
            }
        }

        internal void Tick(float unscaledDeltaTime, bool runActive, bool isRunScene)
        {
            if (!runActive || !isRunScene)
            {
                return;
            }

            _anchorResolver.EnsureRuntimeReferences();
            bool anchorChanged = _anchorResolver.UpdateStationAnchorForCurrentRegion(false);
            if (anchorChanged)
            {
                StopRefueling();
                ClearInteractPoint();
            }

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

            if (_fuel == null || _fuel.Fuel01 >= 0.999f)
            {
                StopRefueling();
                return;
            }

            if (_economy == null)
            {
                _services.TryGet(out _economy);
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
                _events.Publish(new SessionBalanceChanged
                {
                    Balance = _economy.SessionBalance,
                    Delta = -charge,
                    Reason = "fuel_refill"
                });
            }

            _fuel.AddFuel(FuelPerSecond * unscaledDeltaTime);
            _events.Publish(new FuelStateChanged
            {
                Fuel01 = _fuel.Fuel01,
                CurrentFuel = _fuel.CurrentFuel,
                MaxFuel = _fuel.MaxFuel
            });
        }

        private void EnsureStationInteractPoint()
        {
            OrderBuildingAnchor stationAnchor = _anchorResolver.CurrentAnchor;
            if (_interactPoint != null || stationAnchor == null)
            {
                return;
            }

            if (!_anchorResolver.TryResolveRoadSidePosition(stationAnchor.transform.position, out Vector3 spawnPosition))
            {
                return;
            }

            GameObject interactObject = new GameObject("FuelStationInteractPoint");
            interactObject.transform.position = spawnPosition;
            interactObject.transform.rotation = Quaternion.identity;

            _interactPoint = interactObject.AddComponent<FuelStationInteractPoint>();
            _interactPoint.Configure(GetStationDisplayName(stationAnchor), InteractRadius);
            _interactPoint.Arm();
            _interactPoint.InteractRequested += OnFuelInteractRequested;

            FuelStationInteractVisualFactory.AddInteractVisual(interactObject.transform);
        }

        private void OnFuelInteractRequested(FuelStationInteractPoint point)
        {
            if (point == null)
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
                _services.TryGet(out _economy);
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
            _events.Publish(new FuelRefuelStateChanged
            {
                IsRefueling = isRefueling,
                CostPerSecond = CostPerSecond,
                FuelPerSecond = FuelPerSecond
            });
        }

        private void CleanupSceneObjects()
        {
            ClearInteractPoint();
            _anchorResolver.CleanupSceneState();
        }

        private void ClearInteractPoint()
        {
            if (_interactPoint == null)
            {
                return;
            }

            _interactPoint.InteractRequested -= OnFuelInteractRequested;
            GameObject go = _interactPoint.gameObject;
            _interactPoint = null;
            if (go != null)
            {
                Object.Destroy(go);
            }
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
