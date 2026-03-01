using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class TrafficSignalVisualRuntime
    {
        private struct SignalVisual
        {
            public int NodeId;
            public Renderer VerticalRed;
            public Renderer VerticalYellow;
            public Renderer VerticalGreen;
            public Renderer HorizontalRed;
            public Renderer HorizontalYellow;
            public Renderer HorizontalGreen;
        }

        private const float SignalCornerOffset = 8.5f;
        private const float SignalBaseYOffset = 0.05f;
        private const string SignalTemplateName = "TrafficSignalTemplate";

        private readonly List<SignalVisual> _visuals = new List<SignalVisual>(64);

        private TrafficSignalService _signals;
        private Transform _visualRoot;
        private GameObject _signalTemplate;

        private Material _offMat;
        private Material _redMat;
        private Material _yellowMat;
        private Material _greenMat;

        internal void Bind(TrafficSignalService signals)
        {
            _signals = signals;
        }

        internal void TickVisuals()
        {
            if (_visuals.Count == 0 || _signals == null)
            {
                return;
            }

            for (int i = 0; i < _visuals.Count; i++)
            {
                SignalVisual visual = _visuals[i];
                TrafficSignalState state;
                if (!_signals.TryGetState(visual.NodeId, out state))
                {
                    continue;
                }

                ApplyPhaseVisual(ref visual, state.Phase);
            }
        }

        internal void RebuildVisuals(TrafficRoadNetworkService network)
        {
            DestroyVisuals();

            if (network == null || network.NodeCount <= 0)
            {
                return;
            }

            EnsureMaterials();
            _signalTemplate = GameObject.Find(SignalTemplateName);

            GameObject root = new GameObject("TrafficSignalVisualRoot");
            _visualRoot = root.transform;

            for (int nodeId = 0; nodeId < network.NodeCount; nodeId++)
            {
                TrafficNodeData node;
                if (!network.TryGetNode(nodeId, out node) || !node.IsIntersection)
                {
                    continue;
                }

                SignalVisual visual = CreateSignalVisual(nodeId, node.Position, _visualRoot, _signalTemplate);
                _visuals.Add(visual);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[TrafficSignalManager] Created temp traffic lights: " + _visuals.Count);
#endif
        }

        internal void DestroyVisuals()
        {
            _visuals.Clear();

            if (_visualRoot != null)
            {
                Object.Destroy(_visualRoot.gameObject);
                _visualRoot = null;
            }
        }

        internal void Shutdown()
        {
            DestroyVisuals();
            DestroyMaterials();
            _signals = null;
        }
    }
}
