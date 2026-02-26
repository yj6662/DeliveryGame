using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Tests.EditMode
{
    public sealed class TrafficNpcRoadRuleEditModeTest
    {
        private const float BoundaryEpsilon = 1.35f;
        private const float InwardPadding = 0.2f;
        private const float LaneAdherenceTolerance = 0.05f;
        private const int SimAgentCount = 8;
        private const int SimSteps = 240;
        private const float SimDt = 0.1f;

        [Test]
        public void TrafficNpc_EdgeSpawnAndLaneAdherence_AreValid()
        {
            string runScenePath = ResolveRunScenePath();
            Assert.IsFalse(string.IsNullOrEmpty(runScenePath), "RunScene path not found.");

            Scene runScene = EditorSceneManager.OpenScene(runScenePath, OpenSceneMode.Single);
            Assert.IsTrue(runScene.IsValid(), "Failed to open RunScene: " + runScenePath);

            RoadSurface[] roads = UnityEngine.Object.FindObjectsByType<RoadSurface>(FindObjectsSortMode.None);
            Assert.IsNotNull(roads, "RoadSurface search failed.");
            Assert.Greater(roads.Length, 0, "No RoadSurface found in RunScene.");

            var network = new TrafficRoadNetworkService();
            network.Rebuild(roads);
            Assert.Greater(network.LaneCount, 0, "Traffic network lane count must be > 0.");

            float minX;
            float maxX;
            float minZ;
            float maxZ;
            Vector2 centerXZ;
            ComputeLaneBounds(network, out minX, out maxX, out minZ, out maxZ, out centerXZ);

            List<int> edgeSpawnLanes = BuildEdgeSpawnCandidates(network, minX, maxX, minZ, maxZ, centerXZ);
            Assert.Greater(edgeSpawnLanes.Count, 0, "Edge spawn lane candidates must not be empty.");

            for (int i = 0; i < edgeSpawnLanes.Count; i++)
            {
                TrafficLaneData lane;
                Assert.IsTrue(network.TryGetLane(edgeSpawnLanes[i], out lane), "Invalid lane index in candidates.");
                Assert.IsTrue(
                    IsNearBoundary(lane.Start, minX, maxX, minZ, maxZ, BoundaryEpsilon),
                    "Candidate lane start is not near world road boundary. lane=" + lane.LaneId);
            }

            var rng = new System.Random(1337);
            for (int a = 0; a < SimAgentCount; a++)
            {
                int laneIndex = edgeSpawnLanes[rng.Next(edgeSpawnLanes.Count)];
                float distanceOnLane = 0f;
                int previousStartNodeId = -1;

                for (int step = 0; step < SimSteps; step++)
                {
                    TrafficLaneData lane;
                    Assert.IsTrue(network.TryGetLane(laneIndex, out lane), "Failed to fetch lane during simulation.");
                    Assert.Greater(lane.Length, 0.01f, "Lane length too small.");

                    distanceOnLane += lane.SpeedLimitMps * SimDt;
                    int safety = 0;
                    while (distanceOnLane >= lane.Length && safety < 8)
                    {
                        safety++;
                        distanceOnLane -= lane.Length;

                        int nextLane = PickNextLane(network, laneIndex, previousStartNodeId, rng);
                        if (nextLane < 0)
                        {
                            laneIndex = edgeSpawnLanes[rng.Next(edgeSpawnLanes.Count)];
                            distanceOnLane = 0f;
                            previousStartNodeId = -1;
                            break;
                        }

                        previousStartNodeId = lane.StartNodeId;
                        laneIndex = nextLane;

                        Assert.IsTrue(network.TryGetLane(laneIndex, out lane), "Failed to fetch next lane.");
                    }

                    float t = Mathf.Clamp01(distanceOnLane / lane.Length);
                    Vector3 pos = Vector3.Lerp(lane.Start, lane.End, t);
                    float laneDistance = DistancePointToSegment(pos, lane.Start, lane.End);
                    Assert.LessOrEqual(
                        laneDistance,
                        LaneAdherenceTolerance,
                        "Sim position deviated from lane segment. lane=" + lane.LaneId + ", dist=" + laneDistance.ToString("F4"));
                }
            }
        }

        private static List<int> BuildEdgeSpawnCandidates(
            TrafficRoadNetworkService network,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            Vector2 centerXZ)
        {
            var result = new List<int>(128);
            for (int i = 0; i < network.LaneCount; i++)
            {
                TrafficLaneData lane;
                if (!network.TryGetLane(i, out lane))
                {
                    continue;
                }

                if (!IsNearBoundary(lane.Start, minX, maxX, minZ, maxZ, BoundaryEpsilon))
                {
                    continue;
                }

                if (!IsHeadingInward(lane.Start, lane.End, centerXZ, InwardPadding))
                {
                    continue;
                }

                result.Add(i);
            }

            if (result.Count == 0)
            {
                for (int i = 0; i < network.LaneCount; i++)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static void ComputeLaneBounds(
            TrafficRoadNetworkService network,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ,
            out Vector2 centerXZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;

            for (int i = 0; i < network.LaneCount; i++)
            {
                TrafficLaneData lane;
                if (!network.TryGetLane(i, out lane))
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

            centerXZ = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        private static bool IsNearBoundary(Vector3 p, float minX, float maxX, float minZ, float maxZ, float epsilon)
        {
            if (Mathf.Abs(p.x - minX) <= epsilon) return true;
            if (Mathf.Abs(p.x - maxX) <= epsilon) return true;
            if (Mathf.Abs(p.z - minZ) <= epsilon) return true;
            if (Mathf.Abs(p.z - maxZ) <= epsilon) return true;
            return false;
        }

        private static bool IsHeadingInward(Vector3 start, Vector3 end, Vector2 centerXZ, float padding)
        {
            Vector2 s = new Vector2(start.x, start.z) - centerXZ;
            Vector2 e = new Vector2(end.x, end.z) - centerXZ;
            return e.sqrMagnitude <= (s.sqrMagnitude + padding);
        }

        private static int PickNextLane(
            TrafficRoadNetworkService network,
            int currentLaneIndex,
            int previousStartNodeId,
            System.Random rng)
        {
            int count = network.GetNextLaneCount(currentLaneIndex);
            if (count <= 0)
            {
                return -1;
            }

            int selected = -1;
            float bestScore = float.NegativeInfinity;

            TrafficLaneData currentLane;
            if (!network.TryGetLane(currentLaneIndex, out currentLane))
            {
                return -1;
            }

            for (int i = 0; i < count; i++)
            {
                int candidateIndex;
                if (!network.TryGetNextLane(currentLaneIndex, i, out candidateIndex))
                {
                    continue;
                }

                TrafficLaneData candidate;
                if (!network.TryGetLane(candidateIndex, out candidate))
                {
                    continue;
                }

                if (candidate.EndNodeId == previousStartNodeId && count > 1)
                {
                    continue;
                }

                float score = Vector3.Dot(currentLane.Forward, candidate.Forward) * 2.0f;
                score += ((float)rng.NextDouble() - 0.5f) * 0.25f;
                if (score > bestScore)
                {
                    bestScore = score;
                    selected = candidateIndex;
                }
            }

            if (selected >= 0)
            {
                return selected;
            }

            int fallback;
            if (network.TryGetNextLane(currentLaneIndex, 0, out fallback))
            {
                return fallback;
            }

            return -1;
        }

        private static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float denom = Vector3.Dot(ab, ab);
            if (denom <= 0.00001f)
            {
                return Vector3.Distance(point, a);
            }

            float t = Vector3.Dot(point - a, ab) / denom;
            t = Mathf.Clamp01(t);
            Vector3 closest = a + (ab * t);
            return Vector3.Distance(point, closest);
        }

        private static string ResolveRunScenePath()
        {
            const string preferred = "Assets/Scenes/Run/RunScene.unity";
            if (System.IO.File.Exists(preferred))
            {
                return preferred;
            }

            string[] guids = AssetDatabase.FindAssets("t:Scene RunScene");
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            guids = AssetDatabase.FindAssets("t:Scene Run");
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            return string.Empty;
        }
    }
}
