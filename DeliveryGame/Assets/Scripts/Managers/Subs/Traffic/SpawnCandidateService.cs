using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class SpawnCandidateService
    {
        private const float EdgeSpawnEpsilon = 1.35f;
        private const float EdgeSpawnFallbackPadding = 0.2f;

        private readonly List<int> _edgeSpawnLaneIndices = new List<int>(128);
        private readonly Dictionary<string, List<int>> _edgeSpawnLaneIndicesByRegion =
            new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> _spawnLaneIndicesByRegion =
            new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _laneCountByRegion =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly TrafficRoadNetworkService _network;

        private bool _hasLaneBounds;
        private float _laneMinX;
        private float _laneMaxX;
        private float _laneMinZ;
        private float _laneMaxZ;
        private Vector3 _laneCenter;

        internal SpawnCandidateService(TrafficRoadNetworkService network)
        {
            _network = network;
        }

        internal int EdgeSpawnLaneCount => _edgeSpawnLaneIndices.Count;

        internal void Clear()
        {
            _edgeSpawnLaneIndices.Clear();
            _edgeSpawnLaneIndicesByRegion.Clear();
            _spawnLaneIndicesByRegion.Clear();
            _laneCountByRegion.Clear();
            _hasLaneBounds = false;
        }

        internal void RebuildSpawnCandidates(string currentPlayerRegionId)
        {
            Clear();

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
                AddRegionLane(_spawnLaneIndicesByRegion, laneRegion, i);
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
                AddRegionLane(_edgeSpawnLaneIndicesByRegion, ResolveLaneRegion(lane.Start, lane.End), i);
            }

            if (_edgeSpawnLaneIndices.Count == 0)
            {
                for (int i = 0; i < _network.LaneCount; i++)
                {
                    _edgeSpawnLaneIndices.Add(i);
                    TrafficLaneData lane;
                    if (_network.TryGetLane(i, out lane))
                    {
                        AddRegionLane(_edgeSpawnLaneIndicesByRegion, ResolveLaneRegion(lane.Start, lane.End), i);
                    }
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string focusRegion = string.IsNullOrEmpty(currentPlayerRegionId) ? "none" : currentPlayerRegionId;
            int focusCount = GetRegionSpawnLaneCount(currentPlayerRegionId);
            Debug.Log(
                "[TrafficNpcManager] Spawn candidates rebuilt. Edge lanes=" + _edgeSpawnLaneIndices.Count +
                ", laneCount=" + _network.LaneCount +
                ", focusRegion=" + focusRegion +
                ", focusRegionLanes=" + focusCount);
#endif
        }

        internal bool IsNearRoadBoundary(Vector3 point)
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

        internal bool IsSpawnHeadingInward(Vector3 start, Vector3 end)
        {
            Vector3 center = _laneCenter;
            float startDist = (new Vector2(start.x - center.x, start.z - center.z)).sqrMagnitude;
            float endDist = (new Vector2(end.x - center.x, end.z - center.z)).sqrMagnitude;
            return endDist <= startDist + EdgeSpawnFallbackPadding;
        }

        internal bool TryPickSpawnLane(out int laneIndex, out TrafficLaneData lane,
            string currentPlayerRegionId, Func<int> nextIntSource, List<int> sourceOverride = null)
        {
            laneIndex = -1;
            lane = default;

            if (_network == null || _network.LaneCount <= 0)
            {
                return false;
            }

            if (_edgeSpawnLaneIndices.Count <= 0)
            {
                RebuildSpawnCandidates(currentPlayerRegionId);
            }

            if (_edgeSpawnLaneIndices.Count <= 0)
            {
                return false;
            }

            List<int> source = sourceOverride ?? GetActiveSpawnLaneSource(currentPlayerRegionId);
            if (source == null || source.Count <= 0)
            {
                return false;
            }

            string forcedRegion = string.IsNullOrEmpty(currentPlayerRegionId)
                ? null
                : NormalizeRegionId(currentPlayerRegionId);
            int start = nextIntSource();
            for (int i = 0; i < source.Count; i++)
            {
                int idx = (start + i) % source.Count;
                int candidateLaneIndex = source[idx];
                TrafficLaneData candidateLane;
                if (!_network.TryGetLane(candidateLaneIndex, out candidateLane))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(forcedRegion))
                {
                    string laneRegion = NormalizeRegionId(ResolveLaneRegion(candidateLane.Start, candidateLane.End));
                    if (!string.Equals(laneRegion, forcedRegion, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                laneIndex = candidateLaneIndex;
                lane = candidateLane;
                return true;
            }

            return false;
        }

        internal List<int> GetActiveSpawnLaneSource(string currentPlayerRegionId)
        {
            if (string.IsNullOrEmpty(currentPlayerRegionId))
            {
                return _edgeSpawnLaneIndices;
            }

            return GetRegionLaneSource(currentPlayerRegionId);
        }

        internal List<int> GetRegionLaneSource(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return null;
            }

            string key = NormalizeRegionId(regionId);

            List<int> regionList;
            if (_edgeSpawnLaneIndicesByRegion.TryGetValue(key, out regionList) &&
                regionList != null &&
                regionList.Count > 0)
            {
                return regionList;
            }

            if (_spawnLaneIndicesByRegion.TryGetValue(key, out regionList) &&
                regionList != null &&
                regionList.Count > 0)
            {
                return regionList;
            }

            return null;
        }

        internal int GetRegionLaneCount(string regionId)
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

        internal int GetRegionSpawnLaneCount(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return 0;
            }

            string key = NormalizeRegionId(regionId);
            List<int> lanes;
            if (_edgeSpawnLaneIndicesByRegion.TryGetValue(key, out lanes) && lanes != null && lanes.Count > 0)
            {
                return lanes.Count;
            }

            if (_spawnLaneIndicesByRegion.TryGetValue(key, out lanes) && lanes != null)
            {
                return lanes.Count;
            }

            return 0;
        }

        internal static string ResolveLaneRegion(Vector3 start, Vector3 end)
        {
            Vector3 midpoint = (start + end) * 0.5f;
            return RegionWorldLayout.ResolveRegionId(midpoint);
        }

        internal void IncrementLaneCountForRegion(string regionId)
        {
            string key = NormalizeRegionId(regionId);
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

        internal void AddRegionLane(Dictionary<string, List<int>> targetMap, string regionId, int laneIndex)
        {
            if (targetMap == null)
            {
                return;
            }

            string key = NormalizeRegionId(regionId);
            List<int> list;
            if (!targetMap.TryGetValue(key, out list) || list == null)
            {
                list = new List<int>(32);
                targetMap[key] = list;
            }

            list.Add(laneIndex);
        }

        internal static string NormalizeRegionId(string regionId)
        {
            return string.IsNullOrEmpty(regionId) ? "central" : regionId;
        }
    }
}
