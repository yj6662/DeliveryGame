using System.Collections.Generic;
using DeliveryRun.Delivery.Traffic;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class NpcSpawner
    {
        internal void TrySpawnOne()
        {
            Transform runtimeRoot = _getRuntimeRoot();
            if (runtimeRoot == null)
            {
                return;
            }

            TrafficNpcCatalogSO catalog = _getCatalog();
            if (catalog == null || catalog.VehiclePrefabs == null || catalog.VehiclePrefabs.Length == 0)
            {
                return;
            }

            string region = _getCurrentRegion();
            const int laneTryMax = 24;
            for (int laneTry = 0; laneTry < laneTryMax; laneTry++)
            {
                TrafficLaneData lane;
                int laneIndex;
                if (!_candidates.TryPickSpawnLane(
                        out laneIndex,
                        out lane,
                        region,
                        () => _nextInt(_candidates.GetActiveSpawnLaneSource(region)?.Count ?? 0)))
                {
                    return;
                }

                if (!CanSpawnAt(laneIndex, 0f, lane.Start))
                {
                    continue;
                }

                GameObject prefab = PickPrefab(catalog);
                if (prefab == null)
                {
                    return;
                }

                GameObject instance = Object.Instantiate(
                    prefab,
                    lane.Start,
                    Quaternion.LookRotation(lane.Forward, Vector3.up),
                    runtimeRoot);
                instance.name = prefab.name + "_NPC_" + _states.Count.ToString("00");

                TrafficNpcVehicle vehicle = instance.GetComponent<TrafficNpcVehicle>();
                if (vehicle == null)
                {
                    vehicle = instance.AddComponent<TrafficNpcVehicle>();
                }

                Rigidbody body = instance.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                }

                _states.Add(new NpcRuntimeState
                {
                    Vehicle = vehicle,
                    Transform = instance.transform,
                    Body = body,
                    LaneIndex = laneIndex,
                    DistanceOnLane = 0f,
                    Speed = 0f,
                    DesiredSpeed = 0f,
                    StopForSignal = false,
                    SignalNodeId = -1,
                    PlannedNextLaneIndex = -1,
                    PlannedTurnSpeedFactor = 1f
                });

                return;
            }
        }

        internal bool CanSpawnAt(int laneIndex, float distanceOnLane, Vector3 worldPos, NpcRuntimeState ignoreState = null)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                NpcRuntimeState state = _states[i];
                if (state == null || state.Transform == null || ReferenceEquals(state, ignoreState))
                {
                    continue;
                }

                if (state.LaneIndex == laneIndex)
                {
                    float laneGap = Mathf.Abs(state.DistanceOnLane - distanceOnLane);
                    if (laneGap < MinSpawnGapMeters)
                    {
                        return false;
                    }
                }

                float worldGap = Vector3.Distance(state.Transform.position, worldPos);
                if (worldGap < MinWorldSpawnGapMeters)
                {
                    return false;
                }
            }

            return true;
        }

        internal GameObject PickPrefab(TrafficNpcCatalogSO catalog)
        {
            GameObject[] prefabs = catalog.VehiclePrefabs;
            if (prefabs == null || prefabs.Length == 0)
            {
                return null;
            }

            int start = _nextInt(prefabs.Length);
            for (int i = 0; i < prefabs.Length; i++)
            {
                int idx = (start + i) % prefabs.Length;
                GameObject prefab = prefabs[idx];
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        internal bool ReassignToRandomLane(NpcRuntimeState state, bool restrictToCurrentRegion = false)
        {
            if (_network == null || _network.LaneCount <= 0)
            {
                state.Speed = 0f;
                state.DesiredSpeed = 0f;
                state.DistanceOnLane = 0f;
                state.StopForSignal = false;
                state.SignalNodeId = -1;
                state.PlannedNextLaneIndex = -1;
                state.PlannedTurnSpeedFactor = 1f;
                return false;
            }

            string region = _getCurrentRegion();
            List<int> source = null;
            if (restrictToCurrentRegion)
            {
                source = _candidates.GetRegionLaneSource(region);
                if (source == null || source.Count <= 0)
                {
                    state.Speed = 0f;
                    state.DesiredSpeed = 0f;
                    state.StopForSignal = false;
                    state.SignalNodeId = -1;
                    state.PlannedNextLaneIndex = -1;
                    state.PlannedTurnSpeedFactor = 1f;
                    return false;
                }
            }

            const int tryMax = 12;
            for (int i = 0; i < tryMax; i++)
            {
                TrafficLaneData lane;
                int laneIndex;
                if (!_candidates.TryPickSpawnLane(
                        out laneIndex,
                        out lane,
                        region,
                        () => _nextInt(source?.Count ?? _candidates.GetActiveSpawnLaneSource(region)?.Count ?? 0),
                        source))
                {
                    break;
                }

                if (!CanSpawnAt(laneIndex, 0f, lane.Start, state))
                {
                    continue;
                }

                state.LaneIndex = laneIndex;
                state.DistanceOnLane = 0f;
                state.Speed = 0f;
                state.DesiredSpeed = 0f;
                state.StopForSignal = false;
                state.SignalNodeId = -1;
                state.PlannedNextLaneIndex = -1;
                state.PlannedTurnSpeedFactor = 1f;

                if (state.Transform != null)
                {
                    Quaternion rot = Quaternion.LookRotation(lane.Forward, Vector3.up);
                    if (state.Body != null && state.Body.isKinematic)
                    {
                        state.Body.MovePosition(lane.Start);
                        state.Body.MoveRotation(rot);
                    }
                    else
                    {
                        state.Transform.SetPositionAndRotation(lane.Start, rot);
                    }
                }

                return true;
            }

            state.Speed = 0f;
            state.DesiredSpeed = 0f;
            state.StopForSignal = false;
            state.SignalNodeId = -1;
            state.PlannedNextLaneIndex = -1;
            state.PlannedTurnSpeedFactor = 1f;
            return false;
        }
    }
}
