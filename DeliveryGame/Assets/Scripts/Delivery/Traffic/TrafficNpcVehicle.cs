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

        public string VehicleId => vehicleId;
        public string DisplayName => displayName;
        public TrafficVehicleClass VehicleClass => vehicleClass;
        public float CruiseSpeed => cruiseSpeed;
        public float MaxSpeed => maxSpeed;
    }
}
