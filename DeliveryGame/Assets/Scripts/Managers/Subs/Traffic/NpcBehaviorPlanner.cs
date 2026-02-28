using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class NpcBehaviorPlanner
    {
        private const float HeadwaySeconds = 1.45f;
        private const float MinFollowingGap = 4.2f;
        private const float IntersectionApproachBase = 5.5f;
        private const float IntersectionApproachSpeedMul = 0.42f;
        private const float SignalStopBuffer = 0.9f;
        private const float YellowProceedDistanceMin = 4f;
        private const float YellowProceedSpeedMul = 0.65f;
        private const float TurnApproachDistance = 18f;
        private const float IntersectionExitBlockGap = 5.5f;
        private const float TurnStraightWeight = 0.62f;
        private const float TurnRightWeight = 0.25f;
        private const float TurnLeftWeight = 0.13f;

        private readonly TrafficRoadNetworkService _network;
        private readonly TrafficSignalService _signals;
        private readonly List<NpcRuntimeState> _states;
        private readonly Func<float> _next01;

        internal NpcBehaviorPlanner(
            TrafficRoadNetworkService network,
            TrafficSignalService signals,
            List<NpcRuntimeState> states,
            Func<float> next01)
        {
            _network = network;
            _signals = signals;
            _states = states;
            _next01 = next01;
        }

        internal void UpdateDesiredSpeeds()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null)
                {
                    continue;
                }

                state.StopForSignal = false;
                state.SignalNodeId = -1;

                TrafficLaneData lane;
                if (!_network.TryGetLane(state.LaneIndex, out lane))
                {
                    state.DesiredSpeed = 0f;
                    state.PlannedNextLaneIndex = -1;
                    state.PlannedTurnSpeedFactor = 1f;
                    continue;
                }

                float speedLimit = lane.SpeedLimitMps;
                float cruise = state.Vehicle != null ? state.Vehicle.CruiseSpeed : speedLimit;
                float maxSpeed = state.Vehicle != null ? state.Vehicle.MaxSpeed : speedLimit;

                float desired = Mathf.Min(speedLimit, Mathf.Max(0.1f, maxSpeed), Mathf.Max(0.1f, cruise * 1.25f));

                float remaining = lane.Length - state.DistanceOnLane;
                TurnKind turnKind;
                if (!IsNextLaneOption(state.LaneIndex, state.PlannedNextLaneIndex))
                {
                    state.PlannedNextLaneIndex = PickNextLane(lane, state.LaneIndex, out turnKind);
                    state.PlannedTurnSpeedFactor = GetTurnSpeedFactor(turnKind);
                }
                else
                {
                    TrafficLaneData plannedLane;
                    if (_network.TryGetLane(state.PlannedNextLaneIndex, out plannedLane))
                    {
                        turnKind = ClassifyTurn(lane.Forward, plannedLane.Forward);
                        state.PlannedTurnSpeedFactor = GetTurnSpeedFactor(turnKind);
                    }
                    else
                    {
                        state.PlannedTurnSpeedFactor = 1f;
                    }
                }

                if (state.PlannedTurnSpeedFactor < 0.999f && remaining <= TurnApproachDistance)
                {
                    float turnLimited = speedLimit * state.PlannedTurnSpeedFactor;
                    desired = Mathf.Min(desired, turnLimited);
                }

                float gapAhead = FindGapAheadOnSameLane(i, state.LaneIndex, state.DistanceOnLane);
                if (gapAhead >= 0f)
                {
                    float safeGap = MinFollowingGap + (state.Speed * HeadwaySeconds);
                    if (gapAhead < safeGap)
                    {
                        float ratio = Mathf.Clamp01((gapAhead - 1f) / Mathf.Max(1f, safeGap));
                        desired *= ratio;
                    }
                }

                if (ShouldHoldForBlockedIntersectionExit(i, state, lane, remaining))
                {
                    desired = 0f;
                }

                if (ShouldStopForSignal(state, lane, remaining, out _))
                {
                    desired = 0f;
                    state.StopForSignal = true;
                    state.SignalNodeId = lane.EndNodeId;
                }

                state.DesiredSpeed = Mathf.Max(0f, desired);
            }
        }

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

        internal int PickNextLane(TrafficLaneData currentLane, int currentLaneIndex, out TurnKind chosenTurn)
        {
            chosenTurn = TurnKind.Straight;

            int optionCount = _network.GetNextLaneCount(currentLaneIndex);
            if (optionCount <= 0)
            {
                return -1;
            }

            int straightLane = -1;
            int rightLane = -1;
            int leftLane = -1;
            int fallbackLane = -1;
            float straightBest = float.NegativeInfinity;
            float rightBest = float.NegativeInfinity;
            float leftBest = float.NegativeInfinity;
            float fallbackBest = float.NegativeInfinity;

            for (int option = 0; option < optionCount; option++)
            {
                int candidateIndex;
                if (!_network.TryGetNextLane(currentLaneIndex, option, out candidateIndex))
                {
                    continue;
                }

                TrafficLaneData candidate;
                if (!_network.TryGetLane(candidateIndex, out candidate))
                {
                    continue;
                }

                if (candidate.EndNodeId == currentLane.StartNodeId && optionCount > 1)
                {
                    continue;
                }

                TurnKind turnKind = ClassifyTurn(currentLane.Forward, candidate.Forward);
                float dot = Vector3.Dot(currentLane.Forward, candidate.Forward);
                float score = dot * 2.25f + ((_next01() - 0.5f) * 0.25f);

                if (turnKind == TurnKind.Straight)
                {
                    if (score > straightBest)
                    {
                        straightBest = score;
                        straightLane = candidateIndex;
                    }
                }
                else if (turnKind == TurnKind.Right)
                {
                    if (score > rightBest)
                    {
                        rightBest = score;
                        rightLane = candidateIndex;
                    }
                }
                else if (turnKind == TurnKind.Left)
                {
                    if (score > leftBest)
                    {
                        leftBest = score;
                        leftLane = candidateIndex;
                    }
                }
                else
                {
                    if (score > fallbackBest)
                    {
                        fallbackBest = score;
                        fallbackLane = candidateIndex;
                    }
                }
            }

            float roll = _next01();
            if (straightLane >= 0 && roll < TurnStraightWeight)
            {
                chosenTurn = TurnKind.Straight;
                return straightLane;
            }

            if (rightLane >= 0 && roll < (TurnStraightWeight + TurnRightWeight))
            {
                chosenTurn = TurnKind.Right;
                return rightLane;
            }

            if (leftLane >= 0 && roll < (TurnStraightWeight + TurnRightWeight + TurnLeftWeight))
            {
                chosenTurn = TurnKind.Left;
                return leftLane;
            }

            if (straightLane >= 0)
            {
                chosenTurn = TurnKind.Straight;
                return straightLane;
            }

            if (rightLane >= 0)
            {
                chosenTurn = TurnKind.Right;
                return rightLane;
            }

            if (leftLane >= 0)
            {
                chosenTurn = TurnKind.Left;
                return leftLane;
            }

            if (fallbackLane >= 0)
            {
                chosenTurn = TurnKind.UTurn;
                return fallbackLane;
            }

            int fallback;
            if (_network.TryGetNextLane(currentLaneIndex, 0, out fallback))
            {
                chosenTurn = TurnKind.Straight;
                return fallback;
            }

            return -1;
        }

        internal static TurnKind ClassifyTurn(Vector3 fromForward, Vector3 toForward)
        {
            float dot = Vector3.Dot(fromForward, toForward);
            if (dot >= 0.78f)
            {
                return TurnKind.Straight;
            }

            if (dot <= -0.22f)
            {
                return TurnKind.UTurn;
            }

            float crossY = Vector3.Cross(fromForward, toForward).y;
            if (crossY < 0f)
            {
                return TurnKind.Right;
            }

            return TurnKind.Left;
        }

        internal static float GetTurnSpeedFactor(TurnKind turnKind)
        {
            if (turnKind == TurnKind.Right)
            {
                return 0.74f;
            }

            if (turnKind == TurnKind.Left)
            {
                return 0.64f;
            }

            if (turnKind == TurnKind.UTurn)
            {
                return 0.52f;
            }

            return 1f;
        }
    }
}
