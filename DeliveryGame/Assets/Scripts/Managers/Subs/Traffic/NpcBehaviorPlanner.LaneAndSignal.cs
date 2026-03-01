using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class NpcBehaviorPlanner
    {
        internal bool IsNextLaneOption(int currentLaneIndex, int laneIndex)
        {
            if (laneIndex < 0)
            {
                return false;
            }

            int count = _network.GetNextLaneCount(currentLaneIndex);
            if (count <= 0)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                int candidate;
                if (!_network.TryGetNextLane(currentLaneIndex, i, out candidate))
                {
                    continue;
                }

                if (candidate == laneIndex)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool ShouldHoldForBlockedIntersectionExit(
            int selfIndex,
            NpcRuntimeState state,
            TrafficLaneData lane,
            float remaining)
        {
            if (!_network.IsIntersectionNode(lane.EndNodeId))
            {
                return false;
            }

            if (state.PlannedNextLaneIndex < 0)
            {
                return false;
            }

            float holdDistance = IntersectionApproachBase + (state.Speed * 0.35f);
            if (remaining > holdDistance)
            {
                return false;
            }

            return IsLaneEntryBlocked(selfIndex, state.PlannedNextLaneIndex, IntersectionExitBlockGap);
        }

        internal bool IsLaneEntryBlocked(int selfIndex, int laneIndex, float requiredGap)
        {
            if (laneIndex < 0)
            {
                return false;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                NpcRuntimeState other = _states[i];
                if (other == null || other.Transform == null)
                {
                    continue;
                }

                if (other.LaneIndex != laneIndex)
                {
                    continue;
                }

                if (other.DistanceOnLane < requiredGap)
                {
                    return true;
                }
            }

            return false;
        }

        internal float FindGapAheadOnSameLane(int selfIndex, int laneIndex, float selfDistance)
        {
            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _states.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                NpcRuntimeState other = _states[i];
                if (other == null || other.Transform == null)
                {
                    continue;
                }

                if (other.LaneIndex != laneIndex)
                {
                    continue;
                }

                float delta = other.DistanceOnLane - selfDistance;
                if (delta <= 0f)
                {
                    continue;
                }

                if (delta < nearest)
                {
                    nearest = delta;
                    found = true;
                }
            }

            return found ? nearest : -1f;
        }

        internal bool ShouldStopForSignal(
            NpcRuntimeState state,
            TrafficLaneData lane,
            float remainingDistanceToStopLine,
            out float stopDistanceOnLane)
        {
            stopDistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
            if (_signals == null || !_network.IsIntersectionNode(lane.EndNodeId))
            {
                return false;
            }

            bool isGreen;
            bool isYellow;
            float phaseRemaining;
            bool hasLaneSignal = _signals.TryGetLaneSignal(
                lane.EndNodeId,
                lane.Forward,
                out isGreen,
                out isYellow,
                out phaseRemaining);

            if (!hasLaneSignal)
            {
                return !_signals.CanEnter(lane.EndNodeId, lane.Forward);
            }

            if (isGreen)
            {
                return false;
            }

            if (isYellow)
            {
                float proceedDistance = YellowProceedDistanceMin + (state.Speed * YellowProceedSpeedMul);
                bool alreadyCommitted = remainingDistanceToStopLine <= proceedDistance;
                bool yellowEndingSoon = phaseRemaining <= 0.25f && remainingDistanceToStopLine <= proceedDistance * 1.25f;
                return !(alreadyCommitted || yellowEndingSoon);
            }

            if (state.StopForSignal && state.SignalNodeId == lane.EndNodeId)
            {
                return true;
            }

            float approachDistance = IntersectionApproachBase + (state.Speed * IntersectionApproachSpeedMul);
            return remainingDistanceToStopLine <= approachDistance;
        }
    }
}
