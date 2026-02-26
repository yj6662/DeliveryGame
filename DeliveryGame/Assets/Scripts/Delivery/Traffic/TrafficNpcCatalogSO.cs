using UnityEngine;

namespace DeliveryRun.Delivery.Traffic
{
    [CreateAssetMenu(fileName = "TrafficNpcCatalog", menuName = "DeliveryRun/Traffic/Npc Catalog")]
    public sealed class TrafficNpcCatalogSO : ScriptableObject
    {
        public GameObject[] VehiclePrefabs;
    }
}
