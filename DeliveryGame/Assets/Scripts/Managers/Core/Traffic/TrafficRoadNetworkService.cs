using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.World;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class TrafficRoadNetworkService
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

        private void CollectRoadStrips(RoadSurface[] roadSurfaces)
        {
            for (int i = 0; i < roadSurfaces.Length; i++)
            {
                RoadSurface roadSurface = roadSurfaces[i];
                if (roadSurface == null)
                {
                    continue;
                }

                Collider roadCollider = roadSurface.CachedCollider;
                if (roadCollider == null)
                {
                    roadCollider = roadSurface.GetComponent<Collider>();
                }

                if (roadCollider == null || !roadCollider.enabled)
                {
                    continue;
                }

                if (!roadCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = roadCollider.bounds;
                if (bounds.size.x <= 0.01f || bounds.size.z <= 0.01f)
                {
                    continue;
                }

                bool vertical = bounds.size.z > bounds.size.x;
                float halfWidth = vertical ? bounds.extents.x : bounds.extents.z;
                float min = vertical ? bounds.min.z + RoadEndInset : bounds.min.x + RoadEndInset;
                float max = vertical ? bounds.max.z - RoadEndInset : bounds.max.x - RoadEndInset;

                if ((max - min) < MinSegmentLength)
                {
                    continue;
                }

                var strip = new RoadStripData
                {
                    Vertical = vertical,
                    MainCoord = vertical ? bounds.center.x : bounds.center.z,
                    Min = min,
                    Max = max,
                    HalfWidth = halfWidth,
                    TopY = bounds.max.y + HeightOffset
                };

                if (vertical)
                {
                    _verticalRoads.Add(strip);
                }
                else
                {
                    _horizontalRoads.Add(strip);
                }
            }

            _verticalRoads.Sort((a, b) => a.MainCoord.CompareTo(b.MainCoord));
            _horizontalRoads.Sort((a, b) => a.MainCoord.CompareTo(b.MainCoord));
        }

        private void BuildSegmentedLanes()
        {
            int nextLaneId = 0;

            for (int i = 0; i < _verticalRoads.Count; i++)
            {
                RoadStripData vertical = _verticalRoads[i];
                BuildVerticalSegments(vertical, ref nextLaneId);
            }

            for (int i = 0; i < _horizontalRoads.Count; i++)
            {
                RoadStripData horizontal = _horizontalRoads[i];
                BuildHorizontalSegments(horizontal, ref nextLaneId);
            }
        }

        private void BuildVerticalSegments(RoadStripData strip, ref int nextLaneId)
        {
            _splitBuffer.Clear();
            AddSplit(strip.Min);
            AddSplit(strip.Max);

            for (int i = 0; i < _horizontalRoads.Count; i++)
            {
                RoadStripData horizontal = _horizontalRoads[i];
                if (strip.MainCoord < horizontal.Min || strip.MainCoord > horizontal.Max)
                {
                    continue;
                }

                if (horizontal.MainCoord <= strip.Min || horizontal.MainCoord >= strip.Max)
                {
                    continue;
                }

                AddSplit(horizontal.MainCoord);
            }

            _splitBuffer.Sort();
            float laneOffsetInner;
            float laneOffsetOuter;
            BuildLanePairOffsets(strip.HalfWidth, out laneOffsetInner, out laneOffsetOuter);
            float speedLimit = ComputeSpeedLimit(strip.HalfWidth * 2f);

            for (int i = 0; i < _splitBuffer.Count - 1; i++)
            {
                float segStart = _splitBuffer[i];
                float segEnd = _splitBuffer[i + 1];
                float len = segEnd - segStart;
                if (len < MinSegmentLength)
                {
                    continue;
                }

                Vector3 startNorthInner = new Vector3(strip.MainCoord + laneOffsetInner, strip.TopY, segStart);
                Vector3 endNorthInner = new Vector3(strip.MainCoord + laneOffsetInner, strip.TopY, segEnd);
                AddLane(startNorthInner, endNorthInner, speedLimit, ref nextLaneId);

                Vector3 startNorthOuter = new Vector3(strip.MainCoord + laneOffsetOuter, strip.TopY, segStart);
                Vector3 endNorthOuter = new Vector3(strip.MainCoord + laneOffsetOuter, strip.TopY, segEnd);
                AddLane(startNorthOuter, endNorthOuter, speedLimit, ref nextLaneId);

                Vector3 startSouthInner = new Vector3(strip.MainCoord - laneOffsetInner, strip.TopY, segEnd);
                Vector3 endSouthInner = new Vector3(strip.MainCoord - laneOffsetInner, strip.TopY, segStart);
                AddLane(startSouthInner, endSouthInner, speedLimit, ref nextLaneId);

                Vector3 startSouthOuter = new Vector3(strip.MainCoord - laneOffsetOuter, strip.TopY, segEnd);
                Vector3 endSouthOuter = new Vector3(strip.MainCoord - laneOffsetOuter, strip.TopY, segStart);
                AddLane(startSouthOuter, endSouthOuter, speedLimit, ref nextLaneId);
            }
        }

        private void BuildHorizontalSegments(RoadStripData strip, ref int nextLaneId)
        {
            _splitBuffer.Clear();
            AddSplit(strip.Min);
            AddSplit(strip.Max);

            for (int i = 0; i < _verticalRoads.Count; i++)
            {
                RoadStripData vertical = _verticalRoads[i];
                if (strip.MainCoord < vertical.Min || strip.MainCoord > vertical.Max)
                {
                    continue;
                }

                if (vertical.MainCoord <= strip.Min || vertical.MainCoord >= strip.Max)
                {
                    continue;
                }

                AddSplit(vertical.MainCoord);
            }

            _splitBuffer.Sort();
            float laneOffsetInner;
            float laneOffsetOuter;
            BuildLanePairOffsets(strip.HalfWidth, out laneOffsetInner, out laneOffsetOuter);
            float speedLimit = ComputeSpeedLimit(strip.HalfWidth * 2f);

            for (int i = 0; i < _splitBuffer.Count - 1; i++)
            {
                float segStart = _splitBuffer[i];
                float segEnd = _splitBuffer[i + 1];
                float len = segEnd - segStart;
                if (len < MinSegmentLength)
                {
                    continue;
                }

                Vector3 startEastInner = new Vector3(segStart, strip.TopY, strip.MainCoord - laneOffsetInner);
                Vector3 endEastInner = new Vector3(segEnd, strip.TopY, strip.MainCoord - laneOffsetInner);
                AddLane(startEastInner, endEastInner, speedLimit, ref nextLaneId);

                Vector3 startEastOuter = new Vector3(segStart, strip.TopY, strip.MainCoord - laneOffsetOuter);
                Vector3 endEastOuter = new Vector3(segEnd, strip.TopY, strip.MainCoord - laneOffsetOuter);
                AddLane(startEastOuter, endEastOuter, speedLimit, ref nextLaneId);

                Vector3 startWestInner = new Vector3(segEnd, strip.TopY, strip.MainCoord + laneOffsetInner);
                Vector3 endWestInner = new Vector3(segStart, strip.TopY, strip.MainCoord + laneOffsetInner);
                AddLane(startWestInner, endWestInner, speedLimit, ref nextLaneId);

                Vector3 startWestOuter = new Vector3(segEnd, strip.TopY, strip.MainCoord + laneOffsetOuter);
                Vector3 endWestOuter = new Vector3(segStart, strip.TopY, strip.MainCoord + laneOffsetOuter);
                AddLane(startWestOuter, endWestOuter, speedLimit, ref nextLaneId);
            }
        }

        private static void BuildLanePairOffsets(float halfWidth, out float inner, out float outer)
        {
            inner = Mathf.Clamp(halfWidth * 0.22f, MinLaneOffset, MaxLaneOffset);
            outer = Mathf.Clamp(halfWidth * 0.52f, inner + 0.8f, halfWidth - LaneEdgeSafetyMargin);

            float maxAllowed = halfWidth - LaneEdgeSafetyMargin;
            if (outer > maxAllowed)
            {
                outer = Mathf.Max(inner + 0.5f, maxAllowed);
            }

            if (inner > outer - 0.4f)
            {
                inner = Mathf.Max(MinLaneOffset, outer - 0.8f);
            }
        }

        private void AddSplit(float split)
        {
            for (int i = 0; i < _splitBuffer.Count; i++)
            {
                if (Mathf.Abs(_splitBuffer[i] - split) <= SplitMergeEpsilon)
                {
                    return;
                }
            }

            _splitBuffer.Add(split);
        }

        private static float ComputeSpeedLimit(float roadWidth)
        {
            float t = Mathf.InverseLerp(10f, 24f, roadWidth);
            return Mathf.Lerp(8.5f, 14.5f, t);
        }

        private void AddLane(Vector3 start, Vector3 end, float speedLimit, ref int nextLaneId)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.05f)
            {
                return;
            }

            Vector3 forward = delta / length;
            int startNodeId = GetOrCreateNode(start);
            int endNodeId = GetOrCreateNode(end);

            _lanes.Add(new TrafficLaneData
            {
                LaneId = nextLaneId,
                Start = start,
                End = end,
                Forward = forward,
                Length = length,
                SpeedLimitMps = speedLimit,
                StartNodeId = startNodeId,
                EndNodeId = endNodeId
            });

            AddNodeLaneLink(_outgoingByNode, startNodeId, nextLaneId);
            AddNodeLaneLink(_incomingByNode, endNodeId, nextLaneId);
            nextLaneId++;
        }

        private int GetOrCreateNode(Vector3 position)
        {
            long key = MakeNodeKey(position);
            int existing;
            if (_nodeIndexByKey.TryGetValue(key, out existing))
            {
                return existing;
            }

            int nodeId = _nodes.Count;
            _nodeIndexByKey.Add(key, nodeId);
            _nodes.Add(new TrafficNodeData
            {
                NodeId = nodeId,
                Position = position,
                IsIntersection = false
            });

            return nodeId;
        }

        private static long MakeNodeKey(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x * NodeKeyScale);
            int z = Mathf.RoundToInt(position.z * NodeKeyScale);
            long key = ((long)x << 32) ^ (uint)z;
            return key;
        }

        private static void AddNodeLaneLink(Dictionary<int, List<int>> map, int nodeId, int laneId)
        {
            List<int> list;
            if (!map.TryGetValue(nodeId, out list) || list == null)
            {
                list = new List<int>(6);
                map[nodeId] = list;
            }

            list.Add(laneId);
        }

        private void MarkIntersections()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                TrafficNodeData node = _nodes[i];

                int incoming = 0;
                int outgoing = 0;

                List<int> inList;
                if (_incomingByNode.TryGetValue(i, out inList) && inList != null)
                {
                    incoming = inList.Count;
                }

                List<int> outList;
                if (_outgoingByNode.TryGetValue(i, out outList) && outList != null)
                {
                    outgoing = outList.Count;
                }

                int degree = incoming + outgoing;
                node.IsIntersection = degree >= 4 || (incoming >= 2 && outgoing >= 2);
                _nodes[i] = node;
            }
        }

        private void RebuildSpawnPoints()
        {
            _spawnPoints.Clear();
            for (int i = 0; i < _lanes.Count; i++)
            {
                _spawnPoints.Add(_lanes[i].Start);
            }
        }

        private void AddSyntheticCenterIntersections()
        {
            for (int i = 0; i < _verticalRoads.Count; i++)
            {
                RoadStripData vertical = _verticalRoads[i];

                for (int j = 0; j < _horizontalRoads.Count; j++)
                {
                    RoadStripData horizontal = _horizontalRoads[j];

                    bool verticalCoversHorizontalCenter =
                        horizontal.MainCoord > vertical.Min && horizontal.MainCoord < vertical.Max;
                    if (!verticalCoversHorizontalCenter)
                    {
                        continue;
                    }

                    bool horizontalCoversVerticalCenter =
                        vertical.MainCoord > horizontal.Min && vertical.MainCoord < horizontal.Max;
                    if (!horizontalCoversVerticalCenter)
                    {
                        continue;
                    }

                    Vector3 center = new Vector3(
                        vertical.MainCoord,
                        Mathf.Max(vertical.TopY, horizontal.TopY),
                        horizontal.MainCoord);

                    int nodeId = GetOrCreateNode(center);
                    TrafficNodeData node = _nodes[nodeId];
                    node.IsIntersection = true;
                    _nodes[nodeId] = node;
                }
            }
        }
    }
}
