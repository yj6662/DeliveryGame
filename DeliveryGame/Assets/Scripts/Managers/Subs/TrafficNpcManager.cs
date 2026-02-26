using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class TrafficNpcManager : SubManagerBase
    {
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
        }

        private const string CatalogResourcePath = "Bootstrap/TrafficNpcCatalog";
        private const float ScenePollInterval = 0.5f;
        private const float SpawnInterval = 0.65f;
        private const float MinSpawnGapMeters = 14f;
        private const float MinWorldSpawnGapMeters = 6f;

        private const float Acceleration = 5.5f;
        private const float Braking = 9.5f;
        private const float HeadwaySeconds = 1.45f;
        private const float MinFollowingGap = 4.2f;

        private const float IntersectionApproachBase = 5.5f;
        private const float IntersectionApproachSpeedMul = 0.42f;
        private const float SignalStopBuffer = 0.9f;
        private const float EdgeSpawnEpsilon = 1.35f;
        private const float EdgeSpawnFallbackPadding = 0.2f;

        private const int MinNpcCount = 10;
        private const int MaxNpcCount = 32;

        private readonly List<NpcRuntimeState> _states = new List<NpcRuntimeState>(40);
        private readonly List<int> _edgeSpawnLaneIndices = new List<int>(128);

        private TrafficRoadNetworkService _network;
        private TrafficSignalService _signals;
        private TrafficNpcCatalogSO _catalog;
        private Transform _runtimeRoot;

        private bool _isRunScene;
        private float _scenePollAccum;
        private float _spawnAccum;
        private uint _rngState = 0x5F3759DFu;

        private bool _missingCatalogLogged;
        private bool _missingNetworkLogged;
        private bool _hasLaneBounds;
        private float _laneMinX;
        private float _laneMaxX;
        private float _laneMinZ;
        private float _laneMaxZ;
        private Vector3 _laneCenter;

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
            if (_states.Count >= target)
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
                    SignalNodeId = -1
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
                    continue;
                }

                float speedLimit = lane.SpeedLimitMps;
                float cruise = state.Vehicle != null ? state.Vehicle.CruiseSpeed : speedLimit;
                float maxSpeed = state.Vehicle != null ? state.Vehicle.MaxSpeed : speedLimit;

                float desired = Mathf.Min(speedLimit, Mathf.Max(0.1f, maxSpeed), Mathf.Max(0.1f, cruise * 1.25f));

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

                if (_network.IsIntersectionNode(lane.EndNodeId) && _signals != null)
                {
                    float remaining = lane.Length - state.DistanceOnLane;
                    float stopDistance = IntersectionApproachBase + (state.Speed * IntersectionApproachSpeedMul);
                    bool canEnter = _signals.CanEnter(lane.EndNodeId, lane.Forward);

                    if (!canEnter && remaining <= stopDistance)
                    {
                        desired = 0f;
                        state.StopForSignal = true;
                        state.SignalNodeId = lane.EndNodeId;
                    }
                }

                state.DesiredSpeed = Mathf.Max(0f, desired);
            }
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
                    float stopDistanceOnLane = lane.Length - SignalStopBuffer;
                    if (state.DistanceOnLane >= stopDistanceOnLane)
                    {
                        state.DistanceOnLane = stopDistanceOnLane;
                        state.Speed = 0f;
                    }
                }

                int safety = 0;
                while (state.DistanceOnLane >= lane.Length && safety < 6)
                {
                    safety++;
                    state.DistanceOnLane -= lane.Length;

                    int nextLane = PickNextLane(lane, state.LaneIndex);
                    if (nextLane < 0)
                    {
                        ReassignToRandomLane(state);
                        lane = default;
                        break;
                    }

                    state.LaneIndex = nextLane;
                    state.StopForSignal = false;
                    state.SignalNodeId = -1;

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

        private int PickNextLane(TrafficLaneData currentLane, int currentLaneIndex)
        {
            int optionCount = _network.GetNextLaneCount(currentLaneIndex);
            if (optionCount <= 0)
            {
                return -1;
            }

            int bestLane = -1;
            float bestScore = float.NegativeInfinity;

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

                float dot = Vector3.Dot(currentLane.Forward, candidate.Forward);
                float score = dot * 2.2f;
                score += (Next01() - 0.5f) * 0.35f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLane = candidateIndex;
                }
            }

            if (bestLane >= 0)
            {
                return bestLane;
            }

            int fallback;
            if (_network.TryGetNextLane(currentLaneIndex, 0, out fallback))
            {
                return fallback;
            }

            return -1;
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
            _hasLaneBounds = false;
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
            }

            if (_edgeSpawnLaneIndices.Count == 0)
            {
                for (int i = 0; i < _network.LaneCount; i++)
                {
                    _edgeSpawnLaneIndices.Add(i);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[TrafficNpcManager] Spawn candidates rebuilt. Edge lanes=" + _edgeSpawnLaneIndices.Count +
                      ", laneCount=" + _network.LaneCount);
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

            int start = NextInt(_edgeSpawnLaneIndices.Count);
            for (int i = 0; i < _edgeSpawnLaneIndices.Count; i++)
            {
                int idx = (start + i) % _edgeSpawnLaneIndices.Count;
                int candidateLaneIndex = _edgeSpawnLaneIndices[idx];
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
    }
}
