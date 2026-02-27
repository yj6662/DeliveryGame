using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class TrafficSignalService
    {
        private struct SignalRuntime
        {
            public TrafficSignalPhase Phase;
            public float RemainingSeconds;
        }

        private const float GreenDuration = 14f;
        private const float YellowDuration = 2f;

        private readonly Dictionary<int, SignalRuntime> _signals = new Dictionary<int, SignalRuntime>(64);
        private readonly List<int> _nodeIds = new List<int>(64);

        public int SignalCount => _nodeIds.Count;

        public void Clear()
        {
            _signals.Clear();
            _nodeIds.Clear();
        }

        public void RebuildFromNetwork(TrafficRoadNetworkService network)
        {
            Clear();
            if (network == null)
            {
                return;
            }

            for (int nodeId = 0; nodeId < network.NodeCount; nodeId++)
            {
                TrafficNodeData node;
                if (!network.TryGetNode(nodeId, out node) || !node.IsIntersection)
                {
                    continue;
                }

                TrafficSignalPhase initialPhase = ((nodeId & 1) == 0)
                    ? TrafficSignalPhase.VerticalGreen
                    : TrafficSignalPhase.HorizontalGreen;

                _signals.Add(nodeId, new SignalRuntime
                {
                    Phase = initialPhase,
                    RemainingSeconds = GreenDuration
                });

                _nodeIds.Add(nodeId);
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f || _nodeIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _nodeIds.Count; i++)
            {
                int nodeId = _nodeIds[i];
                SignalRuntime runtime;
                if (!_signals.TryGetValue(nodeId, out runtime))
                {
                    continue;
                }

                float remaining = runtime.RemainingSeconds - unscaledDeltaTime;
                while (remaining <= 0f)
                {
                    runtime.Phase = NextPhase(runtime.Phase);
                    remaining += GetPhaseDuration(runtime.Phase);
                }

                runtime.RemainingSeconds = remaining;
                _signals[nodeId] = runtime;
            }
        }

        public bool CanEnter(int nodeId, Vector3 laneForward)
        {
            SignalRuntime runtime;
            if (!_signals.TryGetValue(nodeId, out runtime))
            {
                return true;
            }

            bool isVerticalLane = Mathf.Abs(laneForward.z) >= Mathf.Abs(laneForward.x);
            if (runtime.Phase == TrafficSignalPhase.VerticalGreen)
            {
                return isVerticalLane;
            }

            if (runtime.Phase == TrafficSignalPhase.HorizontalGreen)
            {
                return !isVerticalLane;
            }

            return false;
        }

        public bool TryGetLaneSignal(
            int nodeId,
            Vector3 laneForward,
            out bool isGreen,
            out bool isYellow,
            out float remainingSeconds)
        {
            isGreen = false;
            isYellow = false;
            remainingSeconds = 0f;

            SignalRuntime runtime;
            if (!_signals.TryGetValue(nodeId, out runtime))
            {
                return false;
            }

            bool isVerticalLane = Mathf.Abs(laneForward.z) >= Mathf.Abs(laneForward.x);
            if (runtime.Phase == TrafficSignalPhase.VerticalGreen)
            {
                isGreen = isVerticalLane;
                isYellow = false;
            }
            else if (runtime.Phase == TrafficSignalPhase.HorizontalGreen)
            {
                isGreen = !isVerticalLane;
                isYellow = false;
            }
            else if (runtime.Phase == TrafficSignalPhase.VerticalYellow)
            {
                isGreen = false;
                isYellow = isVerticalLane;
            }
            else
            {
                isGreen = false;
                isYellow = !isVerticalLane;
            }

            remainingSeconds = runtime.RemainingSeconds;
            return true;
        }

        public bool TryGetState(int nodeId, out TrafficSignalState state)
        {
            SignalRuntime runtime;
            if (!_signals.TryGetValue(nodeId, out runtime))
            {
                state = default;
                return false;
            }

            state = new TrafficSignalState
            {
                NodeId = nodeId,
                Phase = runtime.Phase,
                RemainingSeconds = runtime.RemainingSeconds
            };

            return true;
        }

        private static TrafficSignalPhase NextPhase(TrafficSignalPhase phase)
        {
            switch (phase)
            {
                case TrafficSignalPhase.VerticalGreen:
                    return TrafficSignalPhase.VerticalYellow;
                case TrafficSignalPhase.VerticalYellow:
                    return TrafficSignalPhase.HorizontalGreen;
                case TrafficSignalPhase.HorizontalGreen:
                    return TrafficSignalPhase.HorizontalYellow;
                default:
                    return TrafficSignalPhase.VerticalGreen;
            }
        }

        private static float GetPhaseDuration(TrafficSignalPhase phase)
        {
            if (phase == TrafficSignalPhase.VerticalGreen || phase == TrafficSignalPhase.HorizontalGreen)
            {
                return GreenDuration;
            }

            return YellowDuration;
        }
    }
}
