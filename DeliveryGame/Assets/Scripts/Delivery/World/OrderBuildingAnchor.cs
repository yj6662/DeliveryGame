using UnityEngine;

namespace DeliveryRun.Delivery.World
{
    public enum OrderBuildingRole
    {
        Restaurant = 0,
        Destination = 1
    }

    public sealed class OrderBuildingAnchor : MonoBehaviour
    {
        [SerializeField] private OrderBuildingRole role = OrderBuildingRole.Restaurant;

        public OrderBuildingRole Role
        {
            get => role;
            set => role = value;
        }
    }
}
