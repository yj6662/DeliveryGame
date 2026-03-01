using System.Collections.Generic;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class TrafficNpcRuntime
    {
        private const string CatalogResourcePath = "Bootstrap/TrafficNpcCatalog";
        private const float ScenePollInterval = 0.5f;
        private const float PlayerRegionPollInterval = 0.35f;

        private readonly ServiceRegistry _services;
        private readonly EventBus _events;
        private readonly List<NpcRuntimeState> _states = new List<NpcRuntimeState>(40);

        private TrafficRoadNetworkService _network;
        private TrafficSignalService _signals;
        private TrafficNpcCatalogSO _catalog;
        private Transform _runtimeRoot;

        private SpawnCandidateService _spawnCandidates;
        private NpcSpawner _spawner;
        private NpcBehaviorPlanner _planner;
        private NpcMovementController _movement;
        private TrafficRoadNetworkService _boundNetwork;
        private TrafficSignalService _boundSignals;

        private bool _isRunScene;
        private float _scenePollAccum;
        private float _spawnAccum;
        private float _playerRegionPollAccum;
        private uint _rngState = 0x5F3759DFu;

        private bool _missingCatalogLogged;
        private bool _missingNetworkLogged;
        private bool _missingPlayerLogged;
        private Transform _playerTransform;
        private string _currentPlayerRegionId = string.Empty;

        internal TrafficNpcRuntime(ServiceRegistry services, EventBus events)
        {
            _services = services;
            _events = events;
        }

        internal void Initialize()
        {
            _catalog = Resources.Load<TrafficNpcCatalogSO>(CatalogResourcePath);
            if (_catalog == null)
            {
                _missingCatalogLogged = true;
                Debug.LogError("[TrafficNpcManager] Missing catalog: Resources/" + CatalogResourcePath);
            }

            _services.TryGet(out _network);
            _services.TryGet(out _signals);

            if (_network == null)
            {
                _missingNetworkLogged = true;
                Debug.LogWarning("[TrafficNpcManager] TrafficRoadNetworkService not found yet.");
            }

            HandleSceneChanged(SceneManager.GetActiveScene().name, true);
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (ScenePollUtil.ShouldPoll(ref _scenePollAccum, ScenePollInterval, unscaledDeltaTime))
            {
                HandleSceneChanged(SceneManager.GetActiveScene().name, false);
            }

            if (!_isRunScene)
            {
                return;
            }

            if (_network == null)
            {
                _services.TryGet(out _network);
                if (_network == null)
                {
                    if (!_missingNetworkLogged)
                    {
                        _missingNetworkLogged = true;
                        Debug.LogWarning("[TrafficNpcManager] TrafficRoadNetworkService unavailable.");
                    }

                    return;
                }

                _missingNetworkLogged = false;
            }

            if (_signals == null)
            {
                _services.TryGet(out _signals);
            }

            if (_catalog == null)
            {
                _catalog = Resources.Load<TrafficNpcCatalogSO>(CatalogResourcePath);
            }

            if (_catalog == null || _catalog.VehiclePrefabs == null || _catalog.VehiclePrefabs.Length == 0)
            {
                if (!_missingCatalogLogged)
                {
                    _missingCatalogLogged = true;
                    Debug.LogWarning("[TrafficNpcManager] Catalog has no vehicle prefabs.");
                }

                return;
            }

            _missingCatalogLogged = false;

            if (_network.LaneCount <= 0)
            {
                return;
            }

            EnsureRuntimeControllers();
            if (_spawnCandidates == null || _spawner == null || _planner == null || _movement == null)
            {
                return;
            }

            if (_spawnCandidates.EdgeSpawnLaneCount == 0)
            {
                RebuildSpawnCandidates();
            }

            UpdateCurrentPlayerRegion(unscaledDeltaTime);

            if (Time.timeScale <= 0.0001f)
            {
                return;
            }

            _spawner.EnforceCurrentRegionResidency();
            _spawner.MaintainSpawn(unscaledDeltaTime, ref _spawnAccum);
            _planner.UpdateDesiredSpeeds();
            _movement.UpdateMovement(unscaledDeltaTime);
            _spawner.CleanupDestroyedStates();
        }

        internal void Shutdown()
        {
            ClearRuntime();
        }

        internal void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (SceneNames.IsRunSceneLike(evt.To))
            {
                return;
            }

            _isRunScene = false;
            ClearRuntime();
        }

        internal void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, true);
        }

        internal void OnTrafficSystemDefined(TrafficSystemDefined evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            EnsureRuntimeControllers();
            RebuildSpawnCandidates();
            _spawner?.ValidateStatesAgainstNetwork();
        }

        private void UpdateCurrentPlayerRegion(float unscaledDt)
        {
            _playerRegionPollAccum += unscaledDt;
            bool shouldPoll = _playerTransform == null || _playerRegionPollAccum >= PlayerRegionPollInterval;
            if (!shouldPoll)
            {
                return;
            }

            _playerRegionPollAccum = 0f;
            if (_playerTransform == null)
            {
                MotorbikeController bike = Object.FindAnyObjectByType<MotorbikeController>();
                if (bike == null)
                {
                    if (!_missingPlayerLogged)
                    {
                        _missingPlayerLogged = true;
                        Debug.LogWarning("[TrafficNpcManager] Player bike not found; spawn fallback uses all regions.");
                    }

                    _currentPlayerRegionId = string.Empty;
                    return;
                }

                _playerTransform = bike.transform;
                _missingPlayerLogged = false;
            }

            if (_playerTransform == null)
            {
                _currentPlayerRegionId = string.Empty;
                return;
            }

            _currentPlayerRegionId = RegionWorldLayout.ResolveRegionId(_playerTransform.position);
        }
    }
}
