using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class NpcSpawner
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
    }
}
