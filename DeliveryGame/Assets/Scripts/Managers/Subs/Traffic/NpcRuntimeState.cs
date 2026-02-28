using DeliveryRun.Delivery.Traffic;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal enum TurnKind
    {
        Straight = 0,
        Right = 1,
        Left = 2,
        UTurn = 3
    }

    internal sealed class NpcRuntimeState
    {
        public TrafficNpcVehicle Vehicle;
        public Transform Transform;
        public Rigidbody Body;
        public int LaneIndex;
        public float DistanceOnLane;
        public float Speed;
        public float DesiredSpeed;
        public bool StopForSignal;
        public int SignalNodeId;
        public int PlannedNextLaneIndex;
        public float PlannedTurnSpeedFactor;
    }
}
