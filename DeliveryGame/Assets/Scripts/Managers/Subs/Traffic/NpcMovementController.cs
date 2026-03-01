using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class NpcMovementController
    {
        private const float Acceleration = 5.5f;
        private const float Braking = 9.5f;
        private const float SignalStopBuffer = 0.9f;
        private const float IntersectionExitBlockGap = 5.5f;

        private readonly TrafficRoadNetworkService _network;
        private readonly TrafficSignalService _signals;
        private readonly List<NpcRuntimeState> _states;
        private readonly NpcBehaviorPlanner _planner;
        private readonly NpcSpawner _spawner;

        internal NpcMovementController(
            TrafficRoadNetworkService network,
            TrafficSignalService signals,
            List<NpcRuntimeState> states,
            NpcBehaviorPlanner planner,
            NpcSpawner spawner)
        {
            _network = network;
            _signals = signals;
            _states = states;
            _planner = planner;
            _spawner = spawner;
        }

        internal void UpdateMovement(float dt)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null)
                {
                    continue;
                }

                if (state.Vehicle != null && state.Vehicle.IsForcedStopped)
                {
                    state.Speed = 0f;
                    state.DesiredSpeed = 0f;
                    continue;
                }

                TrafficLaneData lane;
                if (!_network.TryGetLane(state.LaneIndex, out lane))
                {
                    _spawner.ReassignToRandomLane(state);
                    continue;
                }

                float accel = state.DesiredSpeed >= state.Speed ? Acceleration : Braking;
                state.Speed = Mathf.MoveTowards(state.Speed, state.DesiredSpeed, accel * dt);
                state.DistanceOnLane += state.Speed * dt;

                if (state.StopForSignal)
                {
                    bool canEnterNow = true;
                    if (_signals != null && _network.IsIntersectionNode(lane.EndNodeId))
                    {
                        bool isGreen;
                        bool isYellow;
                        float _unused;
                        if (_signals.TryGetLaneSignal(lane.EndNodeId, lane.Forward, out isGreen, out isYellow, out _unused))
                        {
                            if (isGreen)
                            {
                                canEnterNow = true;
                            }
                            else if (isYellow)
                            {
                                float stopLine = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                                bool alreadyRolling = state.Speed > 1.25f;
                                bool alreadyBeyondStopLine = state.DistanceOnLane >= stopLine - 0.2f;
                                canEnterNow = alreadyRolling && alreadyBeyondStopLine;
                            }
                            else
                            {
                                canEnterNow = false;
                            }
                        }
                        else
                        {
                            canEnterNow = _signals.CanEnter(lane.EndNodeId, lane.Forward);
                        }
                    }

                    if (canEnterNow)
                    {
                        state.StopForSignal = false;
                        state.SignalNodeId = -1;
                    }
                    else
                    {
                        float stopDistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                        if (state.DistanceOnLane >= stopDistanceOnLane)
                        {
                            state.DistanceOnLane = stopDistanceOnLane;
                            state.Speed = 0f;
                        }
                    }
                }

                int safety = 0;
                while (state.DistanceOnLane >= lane.Length && safety < 6)
                {
                    float remaining = 0f;
                    if (_planner.ShouldStopForSignal(state, lane, remaining, out _))
                    {
                        state.StopForSignal = true;
                        state.SignalNodeId = lane.EndNodeId;
                        state.DistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                        state.Speed = 0f;
                        break;
                    }

                    safety++;
                    state.DistanceOnLane -= lane.Length;

                    int nextLane = state.PlannedNextLaneIndex;
                    if (!_planner.IsNextLaneOption(state.LaneIndex, nextLane))
                    {
                        nextLane = _planner.PickNextLane(lane, state.LaneIndex, out _);
                    }

                    if (nextLane < 0)
                    {
                        _spawner.ReassignToRandomLane(state);
                        lane = default;
                        break;
                    }

                    if (_planner.IsLaneEntryBlocked(i, nextLane, IntersectionExitBlockGap))
                    {
                        state.StopForSignal = false;
                        state.SignalNodeId = -1;
                        state.DistanceOnLane = Mathf.Max(0f, lane.Length - SignalStopBuffer);
                        state.Speed = 0f;
                        break;
                    }

                    state.LaneIndex = nextLane;
                    state.StopForSignal = false;
                    state.SignalNodeId = -1;
                    state.PlannedNextLaneIndex = -1;
                    state.PlannedTurnSpeedFactor = 1f;

                    if (!_network.TryGetLane(state.LaneIndex, out lane))
                    {
                        _spawner.ReassignToRandomLane(state);
                        lane = default;
                        break;
                    }
                }

                if (lane.Length <= 0.01f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(state.DistanceOnLane / lane.Length);
                Vector3 pos = Vector3.Lerp(lane.Start, lane.End, t);
                Quaternion rot = Quaternion.LookRotation(lane.Forward, Vector3.up);

                if (state.Body != null && state.Body.isKinematic)
                {
                    state.Body.MovePosition(pos);
                    state.Body.MoveRotation(rot);
                }
                else
                {
                    state.Transform.SetPositionAndRotation(pos, rot);
                }
            }
        }
    }
}
