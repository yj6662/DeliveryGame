using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.World;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed partial class TrafficRoadNetworkService
    {
        private struct RoadStripData
        {
            public bool Vertical;
            public float MainCoord;
            public float Min;
            public float Max;
            public float HalfWidth;
            public float TopY;
        }

        private const float HeightOffset = 0.2f;
        private const float RoadEndInset = 1.2f;
        private const float MinSegmentLength = 2.5f;
        private const float SplitMergeEpsilon = 0.2f;
        private const float NodeKeyScale = 5f;
        private const float MinLaneOffset = 1f;
        private const float MaxLaneOffset = 3.8f;
        private const float LaneEdgeSafetyMargin = 0.7f;

        private readonly List<TrafficLaneData> _lanes = new List<TrafficLaneData>(256);
        private readonly List<TrafficNodeData> _nodes = new List<TrafficNodeData>(256);
        private readonly List<Vector3> _spawnPoints = new List<Vector3>(256);

        private readonly List<RoadStripData> _verticalRoads = new List<RoadStripData>(32);
        private readonly List<RoadStripData> _horizontalRoads = new List<RoadStripData>(32);
        private readonly List<float> _splitBuffer = new List<float>(64);

        private readonly Dictionary<long, int> _nodeIndexByKey = new Dictionary<long, int>(512);
        private readonly Dictionary<int, List<int>> _outgoingByNode = new Dictionary<int, List<int>>(512);
        private readonly Dictionary<int, List<int>> _incomingByNode = new Dictionary<int, List<int>>(512);

        public int LaneCount => _lanes.Count;
        public int NodeCount => _nodes.Count;
        public int SpawnPointCount => _spawnPoints.Count;

        public void Clear()
        {
            _lanes.Clear();
            _nodes.Clear();
            _spawnPoints.Clear();
            _verticalRoads.Clear();
            _horizontalRoads.Clear();
            _splitBuffer.Clear();
            _nodeIndexByKey.Clear();
            _outgoingByNode.Clear();
            _incomingByNode.Clear();
        }

        public bool TryGetLane(int index, out TrafficLaneData lane)
        {
            if (index < 0 || index >= _lanes.Count)
            {
                lane = default;
                return false;
            }

            lane = _lanes[index];
            return true;
        }

        public bool TryGetNode(int nodeId, out TrafficNodeData node)
        {
            if (nodeId < 0 || nodeId >= _nodes.Count)
            {
                node = default;
                return false;
            }

            node = _nodes[nodeId];
            return true;
        }

        public bool TryGetSpawnPoint(int index, out Vector3 spawnPoint)
        {
            if (index < 0 || index >= _spawnPoints.Count)
            {
                spawnPoint = Vector3.zero;
                return false;
            }

            spawnPoint = _spawnPoints[index];
            return true;
        }

        public int GetOutgoingLaneCount(int nodeId)
        {
            List<int> list;
            if (!_outgoingByNode.TryGetValue(nodeId, out list) || list == null)
            {
                return 0;
            }

            return list.Count;
        }

        public bool TryGetOutgoingLane(int nodeId, int optionIndex, out int laneIndex)
        {
            laneIndex = -1;
            List<int> list;
            if (!_outgoingByNode.TryGetValue(nodeId, out list) || list == null)
            {
                return false;
            }

            if (optionIndex < 0 || optionIndex >= list.Count)
            {
                return false;
            }

            laneIndex = list[optionIndex];
            return true;
        }

        public int GetNextLaneCount(int laneIndex)
        {
            TrafficLaneData lane;
            if (!TryGetLane(laneIndex, out lane))
            {
                return 0;
            }

            return GetOutgoingLaneCount(lane.EndNodeId);
        }

        public bool TryGetNextLane(int laneIndex, int optionIndex, out int nextLaneIndex)
        {
            nextLaneIndex = -1;
            TrafficLaneData lane;
            if (!TryGetLane(laneIndex, out lane))
            {
                return false;
            }

            return TryGetOutgoingLane(lane.EndNodeId, optionIndex, out nextLaneIndex);
        }

        public bool IsIntersectionNode(int nodeId)
        {
            TrafficNodeData node;
            if (!TryGetNode(nodeId, out node))
            {
                return false;
            }

            return node.IsIntersection;
        }

        public void Rebuild(RoadSurface[] roadSurfaces)
        {
            Clear();
            if (roadSurfaces == null || roadSurfaces.Length == 0)
            {
                return;
            }

            CollectRoadStrips(roadSurfaces);
            BuildSegmentedLanes();
            MarkIntersections();
            AddSyntheticCenterIntersections();
            RebuildSpawnPoints();
        }

    }
}
