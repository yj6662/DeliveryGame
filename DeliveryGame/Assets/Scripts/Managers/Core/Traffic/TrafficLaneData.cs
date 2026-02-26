using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public struct TrafficLaneData
    {
        public int LaneId;
        public Vector3 Start;
        public Vector3 End;
        public Vector3 Forward;
        public float Length;
        public float SpeedLimitMps;
        public int StartNodeId;
        public int EndNodeId;
    }
}
