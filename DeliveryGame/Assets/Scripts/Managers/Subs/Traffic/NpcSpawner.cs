using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class NpcSpawner
    {
        private const float SpawnInterval = 0.65f;
        private const float MinSpawnGapMeters = 14f;
        private const float MinWorldSpawnGapMeters = 6f;
        private const int MinNpcCount = 10;
        private const int MaxNpcCount = 32;
        private const int MinNpcCountCurrentRegion = 16;
        private const int MaxNpcCountCurrentRegion = 56;

        private readonly TrafficRoadNetworkService _network;
        private readonly List<NpcRuntimeState> _states;
        private readonly SpawnCandidateService _candidates;
        private readonly Func<int, int> _nextInt;
        private readonly Func<string> _getCurrentRegion;
        private readonly Func<Transform> _getRuntimeRoot;
        private readonly Func<TrafficNpcCatalogSO> _getCatalog;

        internal NpcSpawner(
            TrafficRoadNetworkService network,
            List<NpcRuntimeState> states,
            SpawnCandidateService candidates,
            Func<int, int> nextInt,
            Func<string> getCurrentRegion,
            Func<Transform> getRuntimeRoot,
            Func<TrafficNpcCatalogSO> getCatalog)
        {
            _network = network;
            _states = states;
            _candidates = candidates;
            _nextInt = nextInt;
            _getCurrentRegion = getCurrentRegion;
            _getRuntimeRoot = getRuntimeRoot;
            _getCatalog = getCatalog;
        }

        internal void MaintainSpawn(float unscaledDt, ref float spawnAccum)
        {
            int target = ComputeTargetNpcCount();
            if (target <= 0)
            {
                return;
            }

            string region = _getCurrentRegion();
            int existing = string.IsNullOrEmpty(region)
                ? _states.Count
                : CountNpcInRegion(region);

            if (existing >= target)
            {
                return;
            }

            spawnAccum += unscaledDt;
            if (spawnAccum < SpawnInterval)
            {
                return;
            }

            spawnAccum = 0f;
            TrySpawnOne();
        }

        internal int ComputeTargetNpcCount()
        {
            int lanes = _network != null ? _network.LaneCount : 0;
            if (lanes <= 0)
            {
                return 0;
            }

            string region = _getCurrentRegion();
            if (!string.IsNullOrEmpty(region))
            {
                int regionLaneCount = _candidates.GetRegionLaneCount(region);
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

        internal int CountNpcInRegion(string regionId)
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

        internal void EnforceCurrentRegionResidency()
        {
            string region = _getCurrentRegion();
            if (string.IsNullOrEmpty(region) || _states.Count <= 0)
            {
                return;
            }

            string targetRegion = SpawnCandidateService.NormalizeRegionId(region);
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null)
                {
                    _states.RemoveAt(i);
                    continue;
                }

                if (IsStateInRegion(state, targetRegion))
                {
                    continue;
                }

                if (ReassignToRandomLane(state, true))
                {
                    continue;
                }

                Object.Destroy(state.Transform.gameObject);
                _states.RemoveAt(i);
            }
        }

        internal bool IsStateInRegion(NpcRuntimeState state, string targetRegion)
        {
            if (state == null || string.IsNullOrEmpty(targetRegion))
            {
                return false;
            }

            TrafficLaneData lane;
            string stateRegion = _network != null && _network.TryGetLane(state.LaneIndex, out lane)
                ? SpawnCandidateService.ResolveLaneRegion(lane.Start, lane.End)
                : RegionWorldLayout.ResolveRegionId(state.Transform.position);

            return string.Equals(SpawnCandidateService.NormalizeRegionId(stateRegion), targetRegion, StringComparison.Ordinal);
        }

        internal void TrySpawnOne()
        {
            Transform runtimeRoot = _getRuntimeRoot();
            if (runtimeRoot == null)
            {
                return;
            }

            TrafficNpcCatalogSO catalog = _getCatalog();
            if (catalog == null || catalog.VehiclePrefabs == null || catalog.VehiclePrefabs.Length == 0)
            {
                return;
            }

            string region = _getCurrentRegion();
            const int laneTryMax = 24;
            for (int laneTry = 0; laneTry < laneTryMax; laneTry++)
            {
                TrafficLaneData lane;
                int laneIndex;
                if (!_candidates.TryPickSpawnLane(out laneIndex, out lane, region,
                    () => _nextInt(_candidates.GetActiveSpawnLaneSource(region)?.Count ?? 0)))
                {
                    return;
                }

                if (!CanSpawnAt(laneIndex, 0f, lane.Start))
                {
                    continue;
                }

                GameObject prefab = PickPrefab(catalog);
                if (prefab == null)
                {
                    return;
                }

                GameObject instance = Object.Instantiate(prefab, lane.Start,
                    Quaternion.LookRotation(lane.Forward, Vector3.up), runtimeRoot);
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

        internal bool CanSpawnAt(int laneIndex, float distanceOnLane, Vector3 worldPos, NpcRuntimeState ignoreState = null)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null || ReferenceEquals(state, ignoreState))
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

        internal GameObject PickPrefab(TrafficNpcCatalogSO catalog)
        {
            GameObject[] prefabs = catalog.VehiclePrefabs;
            if (prefabs == null || prefabs.Length == 0)
            {
                return null;
            }

            int start = _nextInt(prefabs.Length);
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

        internal bool ReassignToRandomLane(NpcRuntimeState state, bool restrictToCurrentRegion = false)
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
                return false;
            }

            string region = _getCurrentRegion();
            List<int> source = null;
            if (restrictToCurrentRegion)
            {
                source = _candidates.GetRegionLaneSource(region);
                if (source == null || source.Count <= 0)
                {
                    state.Speed = 0f;
                    state.DesiredSpeed = 0f;
                    state.StopForSignal = false;
                    state.SignalNodeId = -1;
                    state.PlannedNextLaneIndex = -1;
                    state.PlannedTurnSpeedFactor = 1f;
                    return false;
                }
            }

            const int tryMax = 12;
            for (int i = 0; i < tryMax; i++)
            {
                TrafficLaneData lane;
                int laneIndex;
                if (!_candidates.TryPickSpawnLane(out laneIndex, out lane, region,
                    () => _nextInt(source?.Count ?? _candidates.GetActiveSpawnLaneSource(region)?.Count ?? 0),
                    source))
                {
                    break;
                }

                if (!CanSpawnAt(laneIndex, 0f, lane.Start, state))
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

                return true;
            }

            state.Speed = 0f;
            state.DesiredSpeed = 0f;
            state.StopForSignal = false;
            state.SignalNodeId = -1;
            state.PlannedNextLaneIndex = -1;
            state.PlannedTurnSpeedFactor = 1f;
            return false;
        }

        internal void ValidateStatesAgainstNetwork()
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

        internal void CleanupDestroyedStates()
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
    }
}
