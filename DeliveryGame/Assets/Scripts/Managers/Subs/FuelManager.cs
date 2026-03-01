using System;
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
    public sealed class FuelManager : SubManagerBase
    {
        // ── Consumption constants ──
        private const float MaxFuelDefault = 100f;
        private const float BaseConsumePerSecond = 0.08f;
        private const float SpeedConsumeFactor = 0.02f;
        private const float PublishInterval = 0.1f;
        private const float ScenePollInterval = 0.25f;

        // ── Station constants ──
        private const float InteractRadius = 4.5f;
        private const float SpawnYOffset = 0.3f;
        private const float FuelPerSecond = 26f;
        private const float CostPerSecond = 34f;
        private static Material s_fuelInteractMaterial;

        // ── Consumption state ──
        private FuelService _fuel;
        private MotorbikeController _bike;
        private bool _runActive;
        private bool _isRunScene;
        private bool _depletedSent;
        private float _publishAccum;
        private float _scenePollAccum;
        private float _lastPublishedFuel01 = -1f;

        // ── Station state ──
        private EconomyService _economy;
        private RoadQueryManager _roadQuery;
        private MotorbikeController _player;
        private OrderBuildingAnchor _stationAnchor;
        private FuelStationInteractPoint _interactPoint;
        private bool _isRefueling;
        private bool _missingAnchorLogged;
        private string _currentRegionId = string.Empty;
        private float _costAccum;

        public override string Name => nameof(FuelManager);
        public override int InitOrder => 72;

        protected override void OnInitialize()
        {
            _fuel = new FuelService();
            _fuel.Reset(MaxFuelDefault);
            Services.Register(_fuel);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            _isRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
            _depletedSent = false;
            PublishFuelState(true);

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

            // ── Fuel consumption ──
            if (_bike == null)
            {
                _bike = Object.FindAnyObjectByType<MotorbikeController>();
            }

            float speed = _bike != null ? Mathf.Max(0f, _bike.CurrentSpeed) : 0f;
            float consume = (BaseConsumePerSecond + (speed * SpeedConsumeFactor)) * unscaledDeltaTime;
            _fuel.Consume(consume);

            if (!_depletedSent && _fuel.IsEmpty)
            {
                _depletedSent = true;
                Events.Publish(new FuelDepleted());
            }

            _publishAccum += unscaledDeltaTime;
            if (_publishAccum >= PublishInterval)
            {
                _publishAccum = 0f;
                PublishFuelState(false);
            }

            // ── Station tick ──
            EnsureRuntimeReferences();
            UpdateStationAnchorForCurrentRegion(false);
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
            _bike = null;
            StopRefueling();
            CleanupSceneObjects();
            PublishFuelState(true);
            PublishRefuelState(false);
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            _bike = null;
            _fuel.Reset(MaxFuelDefault);
            _depletedSent = false;
            _publishAccum = 0f;
            _costAccum = 0f;
            _missingAnchorLogged = false;
            _currentRegionId = string.Empty;
            PublishFuelState(true);
            PublishRefuelState(false);
            EnsureRuntimeReferences();
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
            _bike = null;
            _fuel.RefillToFull();
            _depletedSent = false;
            StopRefueling();
            CleanupSceneObjects();
            PublishFuelState(true);
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
                UpdateStationAnchorForCurrentRegion(true);
            }
        }

        // ── Fuel state publishing ──

        private void PublishFuelState(bool force)
        {
            float fuel01 = _fuel.Fuel01;
            if (!force && Mathf.Abs(fuel01 - _lastPublishedFuel01) < 0.0005f)
            {
                return;
            }

            _lastPublishedFuel01 = fuel01;
            Events.Publish(new FuelStateChanged
            {
                Fuel01 = fuel01,
                CurrentFuel = _fuel.CurrentFuel,
                MaxFuel = _fuel.MaxFuel
            });
        }

        // ── Station logic ──

        private void EnsureRuntimeReferences()
        {
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
        }

        private void UpdateStationAnchorForCurrentRegion(bool forceRefresh)
        {
            if (_stationAnchor != null && !_stationAnchor.gameObject.activeInHierarchy)
            {
                _stationAnchor = null;
            }

            if (_player == null)
            {
                return;
            }

            string regionId = ResolvePlayerRegionId();
            bool regionChanged = !string.Equals(regionId, _currentRegionId, StringComparison.Ordinal);
            if (!regionChanged && !forceRefresh && _stationAnchor != null)
            {
                return;
            }

            OrderBuildingAnchor resolved = FindGasAnchorForRegion(regionId);
            bool anchorChanged = resolved != _stationAnchor;
            _currentRegionId = regionId;

            if (anchorChanged || forceRefresh)
            {
                if (_isRefueling)
                {
                    StopRefueling();
                }

                ClearInteractPoint();
            }

            _stationAnchor = resolved;

            if (_stationAnchor == null)
            {
                if (!_missingAnchorLogged)
                {
                    _missingAnchorLogged = true;
                    Debug.LogWarning("[FuelManager] Gas station anchor not found for region: " + regionId);
                }
                return;
            }

            _missingAnchorLogged = false;
        }

        private OrderBuildingAnchor FindGasAnchorForRegion(string regionId)
        {
            OrderBuildingAnchor[] anchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsSortMode.None);
            OrderBuildingAnchor fallback = null;
            for (int i = 0; i < anchors.Length; i++)
            {
                OrderBuildingAnchor anchor = anchors[i];
                if (anchor == null || anchor.Role != OrderBuildingRole.GasStation)
                {
                    continue;
                }

                if (string.Equals(anchor.RegionId, regionId, StringComparison.Ordinal))
                {
                    return anchor;
                }

                if (fallback == null)
                {
                    fallback = anchor;
                }
            }

            return fallback;
        }

        private string ResolvePlayerRegionId()
        {
            if (_player == null)
            {
                return "central";
            }

            return RegionWorldLayout.ResolveRegionId(_player.transform.position);
        }

        private void EnsureStationInteractPoint()
        {
            if (_interactPoint != null || _stationAnchor == null)
            {
                return;
            }

            Vector3 spawnPosition;
            if (!TryResolveRoadSidePosition(_stationAnchor.transform.position, out spawnPosition))
            {
                return;
            }

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
                Color tint = new Color(1f, 0.9f, 0.2f, 1f);
                renderer.sharedMaterial = GetFuelInteractMaterial(tint);
            }
        }

        private static Material GetFuelInteractMaterial(Color tint)
        {
            if (s_fuelInteractMaterial != null)
            {
                return s_fuelInteractMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = "FuelInteract_Opaque";
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

            s_fuelInteractMaterial = material;
            return material;
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
            ClearInteractPoint();

            _player = null;
            _stationAnchor = null;
            _currentRegionId = string.Empty;
            _missingAnchorLogged = false;
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

        private bool TryResolveRoadSidePosition(Vector3 reference, out Vector3 roadSidePosition)
        {
            roadSidePosition = reference;

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
                    nearestRoad.y += SpawnYOffset;
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

                Collider c = surface.CachedCollider;
                if (c == null)
                {
                    c = surface.GetComponent<Collider>();
                }

                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 candidate = c.ClosestPoint(reference);
                float sqrDistance = (candidate - reference).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                roadSidePosition = candidate;
                found = true;
            }

            if (found)
            {
                roadSidePosition.y += SpawnYOffset;
            }

            return found;
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
