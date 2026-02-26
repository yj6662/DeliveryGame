using System;
using System.Collections;
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
    public sealed class TrafficSignalFlowPlayModeTest
    {
        private const float SceneTimeout = 20f;

        [UnityTest]
        public IEnumerator TrafficSignal_RunScene_VisualsExist_AndPhaseChanges()
        {
            CoreRoot root = null;
            EventBus events = null;
            TrafficRoadNetworkService network = null;
            TrafficSignalService signals = null;

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

                        bool hasNetwork = root.Services.TryGet(out network);
                        bool hasSignals = root.Services.TryGet(out signals);
                        if (!hasNetwork || !hasSignals || network == null || signals == null)
                        {
                            return false;
                        }

                        return network.LaneCount > 0 && signals.SignalCount > 0;
                    },
                    12f,
                    "Traffic network and signals ready");

                Assert.IsNotNull(network, "TrafficRoadNetworkService missing.");
                Assert.IsNotNull(signals, "TrafficSignalService missing.");
                Assert.Greater(network.LaneCount, 0, "LaneCount must be > 0.");
                Assert.Greater(signals.SignalCount, 0, "SignalCount must be > 0.");

                yield return WaitFor(
                    () =>
                    {
                        GameObject rootGo = GameObject.Find("TrafficSignalVisualRoot");
                        return rootGo != null && rootGo.transform.childCount > 0;
                    },
                    6f,
                    "Traffic signal visual root created");

                GameObject signalVisualRoot = GameObject.Find("TrafficSignalVisualRoot");
                Assert.IsNotNull(signalVisualRoot, "TrafficSignalVisualRoot not found.");
                Assert.Greater(signalVisualRoot.transform.childCount, 0, "No signal visuals were created.");

                int targetIntersectionNodeId = FindIntersectionNodeId(network);
                Assert.GreaterOrEqual(targetIntersectionNodeId, 0, "No intersection node found for signal checks.");

                TrafficSignalState firstState;
                bool gotState = signals.TryGetState(targetIntersectionNodeId, out firstState);
                Assert.IsTrue(gotState, "Failed to read initial signal state.");

                GameObject signalNode = signalVisualRoot.transform.Find("Signal_" + targetIntersectionNodeId)?.gameObject;
                Assert.IsNotNull(signalNode, "Signal visual for target node not found.");

                Renderer verticalGreenRenderer = FindRendererByName(signalNode.transform, "Vertical_Green");
                Renderer horizontalGreenRenderer = FindRendererByName(signalNode.transform, "Horizontal_Green");
                Assert.IsNotNull(verticalGreenRenderer, "Vertical_Green renderer missing.");
                Assert.IsNotNull(horizontalGreenRenderer, "Horizontal_Green renderer missing.");

                Material vMatBefore = verticalGreenRenderer.sharedMaterial;
                Material hMatBefore = horizontalGreenRenderer.sharedMaterial;

                bool phaseChanged = false;
                float waitStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - waitStart <= 24f)
                {
                    TrafficSignalState nowState;
                    if (signals.TryGetState(targetIntersectionNodeId, out nowState) &&
                        nowState.Phase != firstState.Phase)
                    {
                        phaseChanged = true;
                        break;
                    }

                    yield return null;
                }

                Assert.IsTrue(phaseChanged, "Traffic signal phase did not change within timeout.");

                bool visualChanged = false;
                float visualWaitStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - visualWaitStart <= 3f)
                {
                    Material vMatNow = verticalGreenRenderer.sharedMaterial;
                    Material hMatNow = horizontalGreenRenderer.sharedMaterial;
                    if (vMatNow != vMatBefore || hMatNow != hMatBefore)
                    {
                        visualChanged = true;
                        break;
                    }

                    yield return null;
                }

                Assert.IsTrue(visualChanged, "Signal visual materials did not react to phase change.");

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

        private static int FindIntersectionNodeId(TrafficRoadNetworkService network)
        {
            for (int i = 0; i < network.NodeCount; i++)
            {
                TrafficNodeData node;
                if (!network.TryGetNode(i, out node))
                {
                    continue;
                }

                if (node.IsIntersection)
                {
                    return i;
                }
            }

            return -1;
        }

        private static Renderer FindRendererByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (string.Equals(r.gameObject.name, name, StringComparison.Ordinal))
                {
                    return r;
                }
            }

            return null;
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
