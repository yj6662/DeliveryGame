using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class NpcBehaviorPlanner
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
    }
}
