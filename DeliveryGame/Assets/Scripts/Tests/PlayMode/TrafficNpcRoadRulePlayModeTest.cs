using System;
using System.Collections;
using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DeliveryRun.PlayModeTests
{
    public sealed class TrafficNpcRoadRulePlayModeTest
    {
        private const float SceneTimeout = 20f;
        private const float SpawnCollectTimeout = 18f;
        private const int MinTrackedNpcCount = 4;
        private const float SpawnBoundaryTolerance = 3.0f;
        private const float LaneAdherenceTolerance = 4.5f;

        [UnityTest]
        public IEnumerator TrafficNpc_SpawnAtRoadEnds_AndStayOnTrafficLanes()
        {
            CoreRoot root = null;
            EventBus events = null;
            TrafficRoadNetworkService network = null;

            try
            {
                Time.timeScale = 1f;

                yield return SceneManager.LoadSceneAsync(SceneNames.CoreScene, LoadSceneMode.Single);
                yield return WaitFor(
                    () =>
                    {
                        root = CoreRoot.Instance;
                        return root != null && root.Events != null;
                    },
                    10f,
                    "CoreRoot initialized");

                events = root.Events;
                Assert.IsNotNull(events, "EventBus missing.");

                if (SceneManager.GetActiveScene().name != SceneNames.LobbyScene)
                {
                    events.Publish(new BootToLobbyRequested());
                }

                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.LobbyScene,
                    SceneTimeout,
                    "Lobby scene loaded");

                events.Publish(new StartRunRequested());
                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.RunScene,
                    SceneTimeout,
                    "Run scene loaded");

                yield return DismissMusicModalIfNeeded();
                yield return WaitFor(() => Time.timeScale > 0.5f, 4f, "Timescale resumed");

                yield return WaitFor(
                    () =>
                    {
                        if (root == null || root.Services == null)
                        {
                            return false;
                        }

                        if (!root.Services.TryGet(out network) || network == null)
                        {
                            return false;
                        }

                        return network.LaneCount > 0;
                    },
                    12f,
                    "Traffic network ready");

                Assert.IsNotNull(network, "TrafficRoadNetworkService missing.");
                Assert.Greater(network.LaneCount, 0, "Traffic lane count must be > 0.");

                float minX;
                float maxX;
                float minZ;
                float maxZ;
                BuildLaneBounds(network, out minX, out maxX, out minZ, out maxZ);

                var firstSeenPositions = new Dictionary<int, Vector3>(16);
                var trackedTransforms = new Dictionary<int, Transform>(16);

                float collectStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - collectStart <= SpawnCollectTimeout)
                {
                    GameObject runtimeRoot = GameObject.Find("TrafficNpcRuntimeRoot");
                    if (runtimeRoot != null)
                    {
                        Transform rootTr = runtimeRoot.transform;
                        for (int i = 0; i < rootTr.childCount; i++)
                        {
                            Transform child = rootTr.GetChild(i);
                            if (child == null)
                            {
                                continue;
                            }

                            int id = child.gameObject.GetInstanceID();
                            if (firstSeenPositions.ContainsKey(id))
                            {
                                continue;
                            }

                            firstSeenPositions.Add(id, child.position);
                            trackedTransforms[id] = child;
                        }
                    }

                    if (firstSeenPositions.Count >= MinTrackedNpcCount)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.GreaterOrEqual(
                    firstSeenPositions.Count,
                    MinTrackedNpcCount,
                    "Did not observe enough traffic NPC spawns.");

                foreach (KeyValuePair<int, Vector3> kv in firstSeenPositions)
                {
                    float edgeDistance = DistanceToBoundaryXZ(kv.Value, minX, maxX, minZ, maxZ);
                    Assert.LessOrEqual(
                        edgeDistance,
                        SpawnBoundaryTolerance,
                        "NPC first-seen position is not near road boundary. id=" + kv.Key + ", edgeDist=" + edgeDistance.ToString("F2"));
                }

                yield return WaitForRealtimeSeconds(3.0f);

                int evaluatedCount = 0;
                int movedCount = 0;

                foreach (KeyValuePair<int, Transform> kv in trackedTransforms)
                {
                    Transform tr = kv.Value;
                    if (tr == null)
                    {
                        continue;
                    }

                    Vector3 start = firstSeenPositions[kv.Key];
                    Vector3 now = tr.position;

                    if (Vector3.Distance(start, now) > 0.45f)
                    {
                        movedCount++;
                    }

                    float laneDistance = DistanceToNearestLane(network, now);
                    Assert.LessOrEqual(
                        laneDistance,
                        LaneAdherenceTolerance,
                        "NPC deviated too far from lanes. id=" + kv.Key + ", laneDist=" + laneDistance.ToString("F2"));

                    evaluatedCount++;
                }

                Assert.Greater(evaluatedCount, 0, "No valid NPC transforms remained for evaluation.");
                Assert.Greater(
                    movedCount,
                    0,
                    "Observed NPCs did not move along lanes.");

                events.Publish(new ReturnToLobbyRequested());
                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.LobbyScene,
                    SceneTimeout,
                    "Return to lobby");
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        private static IEnumerator DismissMusicModalIfNeeded()
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start <= 6f)
            {
                MusicSelectionModalView modal =
                    UnityEngine.Object.FindFirstObjectByType<MusicSelectionModalView>(FindObjectsInactive.Include);
                if (modal != null)
                {
                    Button button = FindFirstSelectButton(modal);
                    if (button != null)
                    {
                        button.onClick.Invoke();
                        yield break;
                    }
                }

                if (Time.timeScale > 0.5f)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static Button FindFirstSelectButton(MusicSelectionModalView modal)
        {
            Button[] buttons = modal.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                Text label = button.GetComponentInChildren<Text>(true);
                if (label == null || string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                if (label.text.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return button;
                }
            }

            return buttons.Length > 0 ? buttons[0] : null;
        }

        private static void BuildLaneBounds(
            TrafficRoadNetworkService network,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ)
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
        }

        private static float DistanceToBoundaryXZ(Vector3 position, float minX, float maxX, float minZ, float maxZ)
        {
            float dxMin = Mathf.Abs(position.x - minX);
            float dxMax = Mathf.Abs(position.x - maxX);
            float dzMin = Mathf.Abs(position.z - minZ);
            float dzMax = Mathf.Abs(position.z - maxZ);
            float d = dxMin;
            if (dxMax < d) d = dxMax;
            if (dzMin < d) d = dzMin;
            if (dzMax < d) d = dzMax;
            return d;
        }

        private static float DistanceToNearestLane(TrafficRoadNetworkService network, Vector3 point)
        {
            float best = float.MaxValue;

            for (int i = 0; i < network.LaneCount; i++)
            {
                TrafficLaneData lane;
                if (!network.TryGetLane(i, out lane))
                {
                    continue;
                }

                float d = DistancePointToSegment(point, lane.Start, lane.End);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
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

        private static IEnumerator WaitForRealtimeSeconds(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float timeoutSecondsRealtime, string conditionName)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start <= timeoutSecondsRealtime)
            {
                bool satisfied;
                try
                {
                    satisfied = predicate();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Wait predicate threw: " + ex);
                    yield break;
                }

                if (satisfied)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                "Timeout waiting for condition: " + conditionName +
                " (" + timeoutSecondsRealtime.ToString("F1") + "s).");
        }
    }
}
