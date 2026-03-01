using UnityEngine;

namespace DeliveryRun.Delivery.Traffic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class TrafficNpcVehicle : MonoBehaviour
    {
        [SerializeField] private string vehicleId = "traffic/default";
        [SerializeField] private string displayName = "Traffic Vehicle";
        [SerializeField] private TrafficVehicleClass vehicleClass = TrafficVehicleClass.Unknown;
        [SerializeField] private float cruiseSpeed = 8f;
        [SerializeField] private float maxSpeed = 12f;

        private float _forcedStopUntilTime;

        public string VehicleId => vehicleId;
        public string DisplayName => displayName;
        public TrafficVehicleClass VehicleClass => vehicleClass;
        public float CruiseSpeed => cruiseSpeed;
        public float MaxSpeed => maxSpeed;
        public bool IsForcedStopped => Time.unscaledTime < _forcedStopUntilTime;

        public void ForceStopForSeconds(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float until = Time.unscaledTime + seconds;
            if (until > _forcedStopUntilTime)
            {
                _forcedStopUntilTime = until;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
