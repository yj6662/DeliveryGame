using System;
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
        private enum TurnKind
        {
            Straight = 0,
            Right = 1,
            Left = 2,
            UTurn = 3
        }

        private sealed class NpcRuntimeState
        {
            public TrafficNpcVehicle Vehicle;
            public Transform Transform;
            public Rigidbody Body;
            public int LaneIndex;
            public float DistanceOnLane;
            public float Speed;
            public float DesiredSpeed;
            public bool StopForSignal;
            public int SignalNodeId;
            public int PlannedNextLaneIndex;
            public float PlannedTurnSpeedFactor;
        }

        private const string CatalogResourcePath = "Bootstrap/TrafficNpcCatalog";
        private const float ScenePollInterval = 0.5f;
        private const float SpawnInterval = 0.65f;
        private const float MinSpawnGapMeters = 14f;
        private const float MinWorldSpawnGapMeters = 6f;
        private const float PlayerRegionPollInterval = 0.35f;

        private const float Acceleration = 5.5f;
        private const float Braking = 9.5f;
        private const float HeadwaySeconds = 1.45f;
        private const float MinFollowingGap = 4.2f;

        private const float IntersectionApproachBase = 5.5f;
        private const float IntersectionApproachSpeedMul = 0.42f;
        private const float SignalStopBuffer = 0.9f;
        private const float YellowProceedDistanceMin = 4f;
        private const float YellowProceedSpeedMul = 0.65f;
        private const float TurnApproachDistance = 18f;
        private const float IntersectionExitBlockGap = 5.5f;
        private const float TurnStraightWeight = 0.62f;
        private const float TurnRightWeight = 0.25f;
        private const float TurnLeftWeight = 0.13f;
        private const float EdgeSpawnEpsilon = 1.35f;
        private const float EdgeSpawnFallbackPadding = 0.2f;

        private const int MinNpcCount = 10;
        private const int MaxNpcCount = 32;
        private const int MinNpcCountCurrentRegion = 16;
        private const int MaxNpcCountCurrentRegion = 56;

        private readonly List<NpcRuntimeState> _states = new List<NpcRuntimeState>(40);
        private readonly List<int> _edgeSpawnLaneIndices = new List<int>(128);
        private readonly Dictionary<string, List<int>> _edgeSpawnLaneIndicesByRegion = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _laneCountByRegion = new Dictionary<string, int>(StringComparer.Ordinal);

        private TrafficRoadNetworkService _network;
        private TrafficSignalService _signals;
        private TrafficNpcCatalogSO _catalog;
        private Transform _runtimeRoot;

        private bool _isRunScene;
        private float _scenePollAccum;
        private float _spawnAccum;
        private float _playerRegionPollAccum;
        private uint _rngState = 0x5F3759DFu;

        private bool _missingCatalogLogged;
        private bool _missingNetworkLogged;
        private bool _missingPlayerLogged;
        private bool _hasLaneBounds;
        private float _laneMinX;
        private float _laneMaxX;
        private float _laneMinZ;
        private float _laneMaxZ;
        private Vector3 _laneCenter;
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

            if (_edgeSpawnLaneIndices.Count == 0)
            {
                RebuildSpawnCandidates();
            }

            UpdateCurrentPlayerRegion(unscaledDeltaTime);

            if (Time.timeScale <= 0.0001f)
            {
                return;
            }

            MaintainSpawn(unscaledDeltaTime);
            UpdateDesiredSpeeds();
            UpdateMovement(unscaledDeltaTime);
            CleanupDestroyedStates();
        }

        protected override void OnShutdown()
        {
            ClearRuntime();
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
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

            RebuildSpawnCandidates();
            ValidateStatesAgainstNetwork();
        }

        private void HandleSceneChanged(string sceneName, bool force)
        {
            bool isRun = sceneName == SceneNames.RunScene;
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

            if (enteredNow || force)
            {
                _spawnAccum = 0f;
                _playerRegionPollAccum = 0f;
                _currentPlayerRegionId = string.Empty;
                _playerTransform = null;
                _missingPlayerLogged = false;
                RebuildSpawnCandidates();
                ValidateStatesAgainstNetwork();
            }
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

        private void MaintainSpawn(float unscaledDt)
        {
            int target = ComputeTargetNpcCount();
            if (target <= 0)
            {
                return;
            }

            int existing = string.IsNullOrEmpty(_currentPlayerRegionId)
                ? _states.Count
                : CountNpcInRegion(_currentPlayerRegionId);

            if (existing >= target)
            {
                return;
            }

            _spawnAccum += unscaledDt;
            if (_spawnAccum < SpawnInterval)
            {
                return;
            }

            _spawnAccum = 0f;
            TrySpawnOne();
        }

        private int ComputeTargetNpcCount()
        {
            int lanes = _network != null ? _network.LaneCount : 0;
            if (lanes <= 0)
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(_currentPlayerRegionId))
            {
                int regionLaneCount = GetRegionLaneCount(_currentPlayerRegionId);
                if (regionLaneCount > 0)
                {
                    int regionTarget = Mathf.CeilToInt(regionLaneCount * 0.75f);
                    if (regionTarget < MinNpcCountCurrentRegion)
                    {
                        regionTarget = MinNpcCountCurrentRegion;
                    }

                    if (regionTarget > MaxNpcCountCurrentRegion)
                    {
                        regionTarget = MaxNpcCountCurrentRegion;
                    }

                    return regionTarget;
                }
            }

            int target = lanes / 4;
            if (target < MinNpcCount)
            {
                target = MinNpcCount;
            }

            if (target > MaxNpcCount)
            {
                target = MaxNpcCount;
            }

            return target;
        }

        private int CountNpcInRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return _states.Count;
            }

            int count = 0;
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null)
                {
                    continue;
                }

                string npcRegion = RegionWorldLayout.ResolveRegionId(state.Transform.position);
                if (string.Equals(npcRegion, regionId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
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

        private void TrySpawnOne()
        {
            if (_runtimeRoot == null)
            {
                EnsureRuntimeRoot();
                if (_runtimeRoot == null)
                {
                    return;
                }
            }

            if (_catalog == null || _catalog.VehiclePrefabs == null || _catalog.VehiclePrefabs.Length == 0)
            {
                return;
            }

            const int laneTryMax = 24;
            for (int laneTry = 0; laneTry < laneTryMax; laneTry++)
            {
                TrafficLaneData lane;
                int laneIndex;
                if (!TryPickSpawnLane(out laneIndex, out lane))
                {
                    return;
                }

                if (!CanSpawnAt(laneIndex, 0f, lane.Start))
                {
                    continue;
                }

                GameObject prefab = PickPrefab();
                if (prefab == null)
                {
                    return;
                }

                GameObject instance = Object.Instantiate(prefab, lane.Start, Quaternion.LookRotation(lane.Forward, Vector3.up), _runtimeRoot);
                instance.name = prefab.name + "_NPC_" + _states.Count.ToString("00");

                TrafficNpcVehicle vehicle = instance.GetComponent<TrafficNpcVehicle>();
                if (vehicle == null)
                {
                    vehicle = instance.AddComponent<TrafficNpcVehicle>();
                }

                Rigidbody body = instance.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                }

                _states.Add(new NpcRuntimeState
                {
                    Vehicle = vehicle,
                    Transform = instance.transform,
                    Body = body,
                    LaneIndex = laneIndex,
                    DistanceOnLane = 0f,
                    Speed = 0f,
                    DesiredSpeed = 0f,
                    StopForSignal = false,
                    SignalNodeId = -1,
                    PlannedNextLaneIndex = -1,
                    PlannedTurnSpeedFactor = 1f
                });

                return;
            }
        }

        private GameObject PickPrefab()
        {
            GameObject[] prefabs = _catalog.VehiclePrefabs;
            if (prefabs == null || prefabs.Length == 0)
            {
                return null;
            }

            int start = NextInt(prefabs.Length);
            for (int i = 0; i < prefabs.Length; i++)
            {
                int idx = (start + i) % prefabs.Length;
                GameObject prefab = prefabs[idx];
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        private bool CanSpawnAt(int laneIndex, float distanceOnLane, Vector3 worldPos)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null)
                {
                    continue;
                }

                if (state.LaneIndex == laneIndex)
                {
                    float laneGap = Mathf.Abs(state.DistanceOnLane - distanceOnLane);
                    if (laneGap < MinSpawnGapMeters)
                    {
                        return false;
                    }
                }

                float worldGap = Vector3.Distance(state.Transform.position, worldPos);
                if (worldGap < MinWorldSpawnGapMeters)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateDesiredSpeeds()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null)
                {
                    continue;
                }

                state.StopForSignal = false;
                state.SignalNodeId = -1;

                TrafficLaneData lane;
                if (!_network.TryGetLane(state.LaneIndex, out lane))
                {
                    state.DesiredSpeed = 0f;
                    state.PlannedNextLaneIndex = -1;
                    state.PlannedTurnSpeedFactor = 1f;
                    continue;
                }

                float speedLimit = lane.SpeedLimitMps;
                float cruise = state.Vehicle != null ? state.Vehicle.CruiseSpeed : speedLimit;
                float maxSpeed = state.Vehicle != null ? state.Vehicle.MaxSpeed : speedLimit;

                float desired = Mathf.Min(speedLimit, Mathf.Max(0.1f, maxSpeed), Mathf.Max(0.1f, cruise * 1.25f));

                float remaining = lane.Length - state.DistanceOnLane;
                TurnKind turnKind;
                if (!IsNextLaneOption(state.LaneIndex, state.PlannedNextLaneIndex))
                {
                    state.PlannedNextLaneIndex = PickNextLane(lane, state.LaneIndex, out turnKind);
                    state.PlannedTurnSpeedFactor = GetTurnSpeedFactor(turnKind);
                }
                else
                {
                    TrafficLaneData plannedLane;
                    if (_network.TryGetLane(state.PlannedNextLaneIndex, out plannedLane))
                    {
                        turnKind = ClassifyTurn(lane.Forward, plannedLane.Forward);
                        state.PlannedTurnSpeedFactor = GetTurnSpeedFactor(turnKind);
                    }
                    else
                    {
                        state.PlannedTurnSpeedFactor = 1f;
                    }
                }

                if (state.PlannedTurnSpeedFactor < 0.999f && remaining <= TurnApproachDistance)
                {
                    float turnLimited = speedLimit * state.PlannedTurnSpeedFactor;
                    desired = Mathf.Min(desired, turnLimited);
                }

                float gapAhead = FindGapAheadOnSameLane(i, state.LaneIndex, state.DistanceOnLane);
                if (gapAhead >= 0f)
                {
                    float safeGap = MinFollowingGap + (state.Speed * HeadwaySeconds);
                    if (gapAhead < safeGap)
                    {
                        float ratio = Mathf.Clamp01((gapAhead - 1f) / Mathf.Max(1f, safeGap));
                        desired *= ratio;
                    }
                }

                if (ShouldHoldForBlockedIntersectionExit(i, state, lane, remaining))
                {
                    desired = 0f;
                }

                if (ShouldStopForSignal(state, lane, remaining, out _))
                {
                    desired = 0f;
                    state.StopForSignal = true;
                    state.SignalNodeId = lane.EndNodeId;
                }

                state.DesiredSpeed = Mathf.Max(0f, desired);
            }
        }

        private bool IsNextLaneOption(int currentLaneIndex, int laneIndex)
        {
            if (laneIndex < 0)
            {
                return false;
            }

            int count = _network.GetNextLaneCount(currentLaneIndex);
            if (count <= 0)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                int candidate;
                if (!_network.TryGetNextLane(currentLaneIndex, i, out candidate))
                {
                    continue;
                }

                if (candidate == laneIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldHoldForBlockedIntersectionExit(
            int selfIndex,
            NpcRuntimeState state,
            TrafficLaneData lane,
            float remaining)
        {
            if (!_network.IsIntersectionNode(lane.EndNodeId))
            {
                return false;
            }

            if (state.PlannedNextLaneIndex < 0)
            {
                return false;
            }

            float holdDistance = IntersectionApproachBase + (state.Speed * 0.35f);
            if (remaining > holdDistance)
            {
                return false;
            }

            return IsLaneEntryBlocked(selfIndex, state.PlannedNextLaneIndex, IntersectionExitBlockGap);
        }

        private bool IsLaneEntryBlocked(int selfIndex, int laneIndex, float requiredGap)
        {
            if (laneIndex < 0)
            {
                return false;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                NpcRuntimeState other = _states[i];
                if (other == null || other.Transform == null)
                {
                    continue;
                }

                if (other.LaneIndex != laneIndex)
                {
                    continue;
                }

                if (other.DistanceOnLane < requiredGap)
                {
                    return true;
                }
            }

            return false;
        }

        private float FindGapAheadOnSameLane(int selfIndex, int laneIndex, float selfDistance)
        {
            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _states.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                NpcRuntimeState other = _states[i];
                if (other == null || other.Transform == null)
                {
                    continue;
                }

                if (other.LaneIndex != laneIndex)
                {
                    continue;
                }

                float delta = other.DistanceOnLane - selfDistance;
                if (delta <= 0f)
                {
                    continue;
                }

                if (delta < nearest)
                {
                    nearest = delta;
                    found = true;
                }
            }

            return found ? nearest : -1f;
        }

        private void UpdateMovement(float dt)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null)
                {
                    continue;
                }

                TrafficLaneData lane;
                if (!_network.TryGetLane(state.LaneIndex, out lane))
                {
                    ReassignToRandomLane(state);
                    continue;
                }

                float accel = state.DesiredSpeed >= state.Speed ? Acceleration : Braking;
                state.Speed = Mathf.MoveTowards(state.Speed, state.DesiredSpeed, accel * dt);
                state.DistanceOnLane += state.Speed * dt;

                if (state.StopForSignal)
                {
                    bool canEnterNow = true;
                    if (_signals != null && _network.IsIntersectionNode(lane.EndNodeId))
                    {
                        bool isGreen;
                        bool isYellow;
                        float _unused;
                        if (_signals.TryGetLaneSignal(lane.EndNodeId, lane.Forward, out isGreen, out isYellow, out _unused))
                        {
                            if (isGreen)
                            {
                                canEnterNow = true;
                            }
                            else if (isYellow)
                            {
                                float stopLine = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                                bool alreadyRolling = state.Speed > 1.25f;
                                bool alreadyBeyondStopLine = state.DistanceOnLane >= stopLine - 0.2f;
                                canEnterNow = alreadyRolling && alreadyBeyondStopLine;
                            }
                            else
                            {
                                canEnterNow = false;
                            }
                        }
                        else
                        {
                            canEnterNow = _signals.CanEnter(lane.EndNodeId, lane.Forward);
                        }
                    }

                    if (canEnterNow)
                    {
                        state.StopForSignal = false;
                        state.SignalNodeId = -1;
                    }
                    else
                    {
                        float stopDistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                        if (state.DistanceOnLane >= stopDistanceOnLane)
                        {
                            state.DistanceOnLane = stopDistanceOnLane;
                            state.Speed = 0f;
                        }
                    }
                }

                int safety = 0;
                while (state.DistanceOnLane >= lane.Length && safety < 6)
                {
                    float remaining = 0f;
                    if (ShouldStopForSignal(state, lane, remaining, out _))
                    {
                        state.StopForSignal = true;
                        state.SignalNodeId = lane.EndNodeId;
                        state.DistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                        state.Speed = 0f;
                        break;
                    }

                    safety++;
                    state.DistanceOnLane -= lane.Length;

                    int nextLane = state.PlannedNextLaneIndex;
                    if (!IsNextLaneOption(state.LaneIndex, nextLane))
                    {
                        nextLane = PickNextLane(lane, state.LaneIndex, out _);
                    }

                    if (nextLane < 0)
                    {
                        ReassignToRandomLane(state);
                        lane = default;
                        break;
                    }

                    if (IsLaneEntryBlocked(i, nextLane, IntersectionExitBlockGap))
                    {
                        state.StopForSignal = false;
                        state.SignalNodeId = -1;
                        state.DistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                        state.Speed = 0f;
                        break;
                    }

                    state.LaneIndex = nextLane;
                    state.StopForSignal = false;
                    state.SignalNodeId = -1;
                    state.PlannedNextLaneIndex = -1;
                    state.PlannedTurnSpeedFactor = 1f;

                    if (!_network.TryGetLane(state.LaneIndex, out lane))
                    {
                        ReassignToRandomLane(state);
                        lane = default;
                        break;
                    }
                }

                if (lane.Length <= 0.01f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(state.DistanceOnLane / lane.Length);
                Vector3 pos = Vector3.Lerp(lane.Start, lane.End, t);
                Quaternion rot = Quaternion.LookRotation(lane.Forward, Vector3.up);

                if (state.Body != null && state.Body.isKinematic)
                {
                    state.Body.MovePosition(pos);
                    state.Body.MoveRotation(rot);
                }
                else
                {
                    state.Transform.SetPositionAndRotation(pos, rot);
                }
            }
        }

        private bool ShouldStopForSignal(
            NpcRuntimeState state,
            TrafficLaneData lane,
            float remainingDistanceToStopLine,
            out float stopDistanceOnLane)
        {
            stopDistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
            if (_signals == null || !_network.IsIntersectionNode(lane.EndNodeId))
            {
                return false;
            }

            bool isGreen;
            bool isYellow;
            float phaseRemaining;
            bool hasLaneSignal = _signals.TryGetLaneSignal(
                lane.EndNodeId,
                lane.Forward,
                out isGreen,
                out isYellow,
                out phaseRemaining);

            if (!hasLaneSignal)
            {
                return !_signals.CanEnter(lane.EndNodeId, lane.Forward);
            }

            if (isGreen)
            {
                return false;
            }

            if (isYellow)
            {
                float proceedDistance = YellowProceedDistanceMin + (state.Speed * YellowProceedSpeedMul);
                bool alreadyCommitted = remainingDistanceToStopLine <= proceedDistance;
                bool yellowEndingSoon = phaseRemaining <= 0.25f && remainingDistanceToStopLine <= proceedDistance * 1.25f;
                return !(alreadyCommitted || yellowEndingSoon);
            }

            if (state.StopForSignal && state.SignalNodeId == lane.EndNodeId)
            {
                return true;
            }

            float approachDistance = IntersectionApproachBase + (state.Speed * IntersectionApproachSpeedMul);
            return remainingDistanceToStopLine <= approachDistance;
        }

        private int PickNextLane(TrafficLaneData currentLane, int currentLaneIndex, out TurnKind chosenTurn)
        {
            chosenTurn = TurnKind.Straight;

            int optionCount = _network.GetNextLaneCount(currentLaneIndex);
            if (optionCount <= 0)
            {
                return -1;
            }

            int straightLane = -1;
            int rightLane = -1;
            int leftLane = -1;
            int fallbackLane = -1;
            float straightBest = float.NegativeInfinity;
            float rightBest = float.NegativeInfinity;
            float leftBest = float.NegativeInfinity;
            float fallbackBest = float.NegativeInfinity;

            for (int option = 0; option < optionCount; option++)
            {
                int candidateIndex;
                if (!_network.TryGetNextLane(currentLaneIndex, option, out candidateIndex))
                {
                    continue;
                }

                TrafficLaneData candidate;
                if (!_network.TryGetLane(candidateIndex, out candidate))
                {
                    continue;
                }

                if (candidate.EndNodeId == currentLane.StartNodeId && optionCount > 1)
                {
                    continue;
                }

                TurnKind turnKind = ClassifyTurn(currentLane.Forward, candidate.Forward);
                float dot = Vector3.Dot(currentLane.Forward, candidate.Forward);
                float score = dot * 2.25f + ((Next01() - 0.5f) * 0.25f);

                if (turnKind == TurnKind.Straight)
                {
                    if (score > straightBest)
                    {
                        straightBest = score;
                        straightLane = candidateIndex;
                    }
                }
                else if (turnKind == TurnKind.Right)
                {
                    if (score > rightBest)
                    {
                        rightBest = score;
                        rightLane = candidateIndex;
                    }
                }
                else if (turnKind == TurnKind.Left)
                {
                    if (score > leftBest)
                    {
                        leftBest = score;
                        leftLane = candidateIndex;
                    }
                }
                else
                {
                    if (score > fallbackBest)
                    {
                        fallbackBest = score;
                        fallbackLane = candidateIndex;
                    }
                }
            }

            float roll = Next01();
            if (straightLane >= 0 && roll < TurnStraightWeight)
            {
                chosenTurn = TurnKind.Straight;
                return straightLane;
            }

            if (rightLane >= 0 && roll < (TurnStraightWeight + TurnRightWeight))
            {
                chosenTurn = TurnKind.Right;
                return rightLane;
            }

            if (leftLane >= 0 && roll < (TurnStraightWeight + TurnRightWeight + TurnLeftWeight))
            {
                chosenTurn = TurnKind.Left;
                return leftLane;
            }

            if (straightLane >= 0)
            {
                chosenTurn = TurnKind.Straight;
                return straightLane;
            }

            if (rightLane >= 0)
            {
                chosenTurn = TurnKind.Right;
                return rightLane;
            }

            if (leftLane >= 0)
            {
                chosenTurn = TurnKind.Left;
                return leftLane;
            }

            if (fallbackLane >= 0)
            {
                chosenTurn = TurnKind.UTurn;
                return fallbackLane;
            }

            int fallback;
            if (_network.TryGetNextLane(currentLaneIndex, 0, out fallback))
            {
                chosenTurn = TurnKind.Straight;
                return fallback;
            }

            return -1;
        }

        private static TurnKind ClassifyTurn(Vector3 fromForward, Vector3 toForward)
        {
            float dot = Vector3.Dot(fromForward, toForward);
            if (dot >= 0.78f)
            {
                return TurnKind.Straight;
            }

            if (dot <= -0.22f)
            {
                return TurnKind.UTurn;
            }

            float crossY = Vector3.Cross(fromForward, toForward).y;
            if (crossY < 0f)
            {
                return TurnKind.Right;
            }

            return TurnKind.Left;
        }

        private static float GetTurnSpeedFactor(TurnKind turnKind)
        {
            if (turnKind == TurnKind.Right)
            {
                return 0.74f;
            }

            if (turnKind == TurnKind.Left)
            {
                return 0.64f;
            }

            if (turnKind == TurnKind.UTurn)
            {
                return 0.52f;
            }

            return 1f;
        }

        private void ReassignToRandomLane(NpcRuntimeState state)
        {
            if (_network == null || _network.LaneCount <= 0)
            {
                state.Speed = 0f;
                state.DesiredSpeed = 0f;
                state.DistanceOnLane = 0f;
                state.StopForSignal = false;
                state.SignalNodeId = -1;
                state.PlannedNextLaneIndex = -1;
                state.PlannedTurnSpeedFactor = 1f;
                return;
            }

            const int tryMax = 12;
            for (int i = 0; i < tryMax; i++)
            {
                TrafficLaneData lane;
                int laneIndex;
                if (!TryPickSpawnLane(out laneIndex, out lane))
                {
                    break;
                }

                if (!CanSpawnAt(laneIndex, 0f, lane.Start))
                {
                    continue;
                }

                state.LaneIndex = laneIndex;
                state.DistanceOnLane = 0f;
                state.Speed = 0f;
                state.DesiredSpeed = 0f;
                state.StopForSignal = false;
                state.SignalNodeId = -1;
                state.PlannedNextLaneIndex = -1;
                state.PlannedTurnSpeedFactor = 1f;

                if (state.Transform != null)
                {
                    Quaternion rot = Quaternion.LookRotation(lane.Forward, Vector3.up);
                    if (state.Body != null && state.Body.isKinematic)
                    {
                        state.Body.MovePosition(lane.Start);
                        state.Body.MoveRotation(rot);
                    }
                    else
                    {
                        state.Transform.SetPositionAndRotation(lane.Start, rot);
                    }
                }

                return;
            }

            state.Speed = 0f;
            state.DesiredSpeed = 0f;
            state.StopForSignal = false;
            state.SignalNodeId = -1;
            state.PlannedNextLaneIndex = -1;
            state.PlannedTurnSpeedFactor = 1f;
        }

        private void CleanupDestroyedStates()
        {
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                NpcRuntimeState state = _states[i];
                if (state != null && state.Transform != null)
                {
                    continue;
                }

                _states.RemoveAt(i);
            }
        }

        private void ValidateStatesAgainstNetwork()
        {
            if (_network == null)
            {
                return;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null)
                {
                    continue;
                }

                TrafficLaneData lane;
                if (_network.TryGetLane(state.LaneIndex, out lane))
                {
                    if (state.DistanceOnLane < lane.Length + 0.01f)
                    {
                        continue;
                    }
                }

                ReassignToRandomLane(state);
            }
        }

        private void ClearRuntime()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null)
                {
                    continue;
                }

                if (state.Transform != null)
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

            _edgeSpawnLaneIndices.Clear();
            _edgeSpawnLaneIndicesByRegion.Clear();
            _laneCountByRegion.Clear();
            _hasLaneBounds = false;
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

        private void RebuildSpawnCandidates()
        {
            _edgeSpawnLaneIndices.Clear();
            _edgeSpawnLaneIndicesByRegion.Clear();
            _laneCountByRegion.Clear();
            _hasLaneBounds = false;

            if (_network == null || _network.LaneCount <= 0)
            {
                return;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            for (int i = 0; i < _network.LaneCount; i++)
            {
                TrafficLaneData lane;
                if (!_network.TryGetLane(i, out lane))
                {
                    continue;
                }

                if (lane.Start.x < minX) minX = lane.Start.x;
                if (lane.Start.x > maxX) maxX = lane.Start.x;
                if (lane.Start.z < minZ) minZ = lane.Start.z;
                if (lane.Start.z > maxZ) maxZ = lane.Start.z;

                if (lane.End.x < minX) minX = lane.End.x;
                if (lane.End.x > maxX) maxX = lane.End.x;
                if (lane.End.z < minZ) minZ = lane.End.z;
                if (lane.End.z > maxZ) maxZ = lane.End.z;

                string laneRegion = ResolveLaneRegion(lane.Start, lane.End);
                IncrementLaneCountForRegion(laneRegion);
            }

            if (minX > maxX || minZ > maxZ)
            {
                return;
            }

            _laneMinX = minX;
            _laneMaxX = maxX;
            _laneMinZ = minZ;
            _laneMaxZ = maxZ;
            _laneCenter = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            _hasLaneBounds = true;

            for (int i = 0; i < _network.LaneCount; i++)
            {
                TrafficLaneData lane;
                if (!_network.TryGetLane(i, out lane))
                {
                    continue;
                }

                if (!IsNearRoadBoundary(lane.Start))
                {
                    continue;
                }

                if (!IsSpawnHeadingInward(lane.Start, lane.End))
                {
                    continue;
                }

                _edgeSpawnLaneIndices.Add(i);
                AddSpawnLaneForRegion(ResolveLaneRegion(lane.Start, lane.End), i);
            }

            if (_edgeSpawnLaneIndices.Count == 0)
            {
                for (int i = 0; i < _network.LaneCount; i++)
                {
                    _edgeSpawnLaneIndices.Add(i);
                    TrafficLaneData lane;
                    if (_network.TryGetLane(i, out lane))
                    {
                        AddSpawnLaneForRegion(ResolveLaneRegion(lane.Start, lane.End), i);
                    }
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string focusRegion = string.IsNullOrEmpty(_currentPlayerRegionId) ? "none" : _currentPlayerRegionId;
            int focusCount = GetRegionSpawnLaneCount(_currentPlayerRegionId);
            Debug.Log(
                "[TrafficNpcManager] Spawn candidates rebuilt. Edge lanes=" + _edgeSpawnLaneIndices.Count +
                ", laneCount=" + _network.LaneCount +
                ", focusRegion=" + focusRegion +
                ", focusRegionLanes=" + focusCount);
#endif
        }

        private bool IsNearRoadBoundary(Vector3 point)
        {
            if (!_hasLaneBounds)
            {
                return false;
            }

            float eps = EdgeSpawnEpsilon;
            if (Mathf.Abs(point.x - _laneMinX) <= eps) return true;
            if (Mathf.Abs(point.x - _laneMaxX) <= eps) return true;
            if (Mathf.Abs(point.z - _laneMinZ) <= eps) return true;
            if (Mathf.Abs(point.z - _laneMaxZ) <= eps) return true;
            return false;
        }

        private bool IsSpawnHeadingInward(Vector3 start, Vector3 end)
        {
            Vector3 center = _laneCenter;
            float startDist = (new Vector2(start.x - center.x, start.z - center.z)).sqrMagnitude;
            float endDist = (new Vector2(end.x - center.x, end.z - center.z)).sqrMagnitude;
            return endDist <= startDist + EdgeSpawnFallbackPadding;
        }

        private bool TryPickSpawnLane(out int laneIndex, out TrafficLaneData lane)
        {
            laneIndex = -1;
            lane = default;

            if (_network == null || _network.LaneCount <= 0)
            {
                return false;
            }

            if (_edgeSpawnLaneIndices.Count <= 0)
            {
                RebuildSpawnCandidates();
            }

            if (_edgeSpawnLaneIndices.Count <= 0)
            {
                return false;
            }

            List<int> source = GetActiveSpawnLaneSource();
            if (source == null || source.Count <= 0)
            {
                source = _edgeSpawnLaneIndices;
            }

            int start = NextInt(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                int idx = (start + i) % source.Count;
                int candidateLaneIndex = source[idx];
                TrafficLaneData candidateLane;
                if (!_network.TryGetLane(candidateLaneIndex, out candidateLane))
                {
                    continue;
                }

                laneIndex = candidateLaneIndex;
                lane = candidateLane;
                return true;
            }

            return false;
        }

        private List<int> GetActiveSpawnLaneSource()
        {
            if (string.IsNullOrEmpty(_currentPlayerRegionId))
            {
                return _edgeSpawnLaneIndices;
            }

            List<int> regionList;
            if (_edgeSpawnLaneIndicesByRegion.TryGetValue(_currentPlayerRegionId, out regionList) &&
                regionList != null &&
                regionList.Count > 0)
            {
                return regionList;
            }

            return _edgeSpawnLaneIndices;
        }

        private int GetRegionLaneCount(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return 0;
            }

            int count;
            if (_laneCountByRegion.TryGetValue(regionId, out count))
            {
                return count;
            }

            return 0;
        }

        private int GetRegionSpawnLaneCount(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return 0;
            }

            List<int> lanes;
            if (_edgeSpawnLaneIndicesByRegion.TryGetValue(regionId, out lanes) && lanes != null)
            {
                return lanes.Count;
            }

            return 0;
        }

        private static string ResolveLaneRegion(Vector3 start, Vector3 end)
        {
            Vector3 midpoint = (start + end) * 0.5f;
            return RegionWorldLayout.ResolveRegionId(midpoint);
        }

        private void IncrementLaneCountForRegion(string regionId)
        {
            string key = string.IsNullOrEmpty(regionId) ? "central" : regionId;
            int count;
            if (_laneCountByRegion.TryGetValue(key, out count))
            {
                _laneCountByRegion[key] = count + 1;
            }
            else
            {
                _laneCountByRegion[key] = 1;
            }
        }

        private void AddSpawnLaneForRegion(string regionId, int laneIndex)
        {
            string key = string.IsNullOrEmpty(regionId) ? "central" : regionId;
            List<int> list;
            if (!_edgeSpawnLaneIndicesByRegion.TryGetValue(key, out list) || list == null)
            {
                list = new List<int>(32);
                _edgeSpawnLaneIndicesByRegion[key] = list;
            }

            list.Add(laneIndex);
        }
    }
}
