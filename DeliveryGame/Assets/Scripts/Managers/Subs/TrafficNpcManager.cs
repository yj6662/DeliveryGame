using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class TrafficNpcManager : SubManagerBase
    {
        private const string CatalogResourcePath = "Bootstrap/TrafficNpcCatalog";
        private const float ScenePollInterval = 0.5f;
        private const float PlayerRegionPollInterval = 0.35f;

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

        public override string Name => nameof(TrafficNpcManager);
        public override int InitOrder => 69;

        protected override void OnInitialize()
        {
            _catalog = Resources.Load<TrafficNpcCatalogSO>(CatalogResourcePath);
            if (_catalog == null)
            {
                _missingCatalogLogged = true;
                Debug.LogError("[TrafficNpcManager] Missing catalog: Resources/" + CatalogResourcePath);
            }

            Services.TryGet(out _network);
            Services.TryGet(out _signals);

            if (_network == null)
            {
                _missingNetworkLogged = true;
                Debug.LogWarning("[TrafficNpcManager] TrafficRoadNetworkService not found yet.");
            }

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<TrafficSystemDefined>(Events, OnTrafficSystemDefined);

            HandleSceneChanged(SceneManager.GetActiveScene().name, true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum >= ScenePollInterval)
            {
                _scenePollAccum = 0f;
                HandleSceneChanged(SceneManager.GetActiveScene().name, false);
            }

            if (!_isRunScene)
            {
                return;
            }

            if (_network == null)
            {
                Services.TryGet(out _network);
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
                Services.TryGet(out _signals);
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

        protected override void OnShutdown()
        {
            ClearRuntime();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (SceneNames.IsRunSceneLike(evt.To))
            {
                return;
            }

            _isRunScene = false;
            ClearRuntime();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, true);
        }

        private void OnTrafficSystemDefined(TrafficSystemDefined evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            EnsureRuntimeControllers();
            RebuildSpawnCandidates();
            _spawner?.ValidateStatesAgainstNetwork();
        }

        private void HandleSceneChanged(string sceneName, bool force)
        {
            bool isRun = SceneNames.IsRunSceneLike(sceneName);
            if (!isRun)
            {
                _isRunScene = false;
                if (force)
                {
                    ClearRuntime();
                }

                return;
            }

            bool enteredNow = !_isRunScene;
            _isRunScene = true;
            EnsureRuntimeRoot();

            if (!enteredNow && !force)
            {
                return;
            }

            _spawnAccum = 0f;
            _playerRegionPollAccum = 0f;
            _currentPlayerRegionId = string.Empty;
            _playerTransform = null;
            _missingPlayerLogged = false;

            EnsureRuntimeControllers();
            RebuildSpawnCandidates();
            _spawner?.ValidateStatesAgainstNetwork();
        }

        private void EnsureRuntimeRoot()
        {
            if (_runtimeRoot != null)
            {
                return;
            }

            GameObject root = GameObject.Find("TrafficNpcRuntimeRoot");
            if (root == null)
            {
                root = new GameObject("TrafficNpcRuntimeRoot");
            }

            _runtimeRoot = root.transform;
        }

        private Transform GetRuntimeRoot()
        {
            EnsureRuntimeRoot();
            return _runtimeRoot;
        }

        private void EnsureRuntimeControllers()
        {
            if (_network == null)
            {
                return;
            }

            bool networkChanged = !ReferenceEquals(_boundNetwork, _network);
            if (networkChanged || _spawnCandidates == null || _spawner == null)
            {
                _spawnCandidates = new SpawnCandidateService(_network);
                _spawner = new NpcSpawner(
                    _network,
                    _states,
                    _spawnCandidates,
                    NextInt,
                    () => _currentPlayerRegionId,
                    GetRuntimeRoot,
                    () => _catalog);

                _boundNetwork = _network;
                _boundSignals = null;
                _planner = null;
                _movement = null;
            }

            bool signalChanged = !ReferenceEquals(_boundSignals, _signals);
            if (signalChanged || _planner == null || _movement == null)
            {
                _planner = new NpcBehaviorPlanner(_network, _signals, _states, Next01);
                _movement = new NpcMovementController(_network, _signals, _states, _planner, _spawner);
                _boundSignals = _signals;
            }
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

        private void RebuildSpawnCandidates()
        {
            _spawnCandidates?.RebuildSpawnCandidates(_currentPlayerRegionId);
        }

        private void ClearRuntime()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state != null && state.Transform != null)
                {
                    Object.Destroy(state.Transform.gameObject);
                }
            }

            _states.Clear();
            _spawnAccum = 0f;

            if (_runtimeRoot != null)
            {
                Object.Destroy(_runtimeRoot.gameObject);
                _runtimeRoot = null;
            }

            _spawnCandidates?.Clear();
            _spawnCandidates = null;
            _spawner = null;
            _planner = null;
            _movement = null;
            _boundNetwork = null;
            _boundSignals = null;

            _playerRegionPollAccum = 0f;
            _currentPlayerRegionId = string.Empty;
            _playerTransform = null;
            _missingPlayerLogged = false;
        }

        private float Next01()
        {
            _rngState = (_rngState * 1664525u) + 1013904223u;
            return (_rngState & 0x00FFFFFFu) / 16777215f;
        }

        private int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
            {
                return 0;
            }

            int value = Mathf.FloorToInt(Next01() * maxExclusive);
            if (value >= maxExclusive)
            {
                value = maxExclusive - 1;
            }

            return value;
        }
    }
}
