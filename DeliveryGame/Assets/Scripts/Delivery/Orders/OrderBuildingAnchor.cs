using UnityEngine;

namespace DeliveryRun.Delivery.Orders
{
    public enum OrderBuildingRole
    {
        Restaurant = 0,
        Destination = 1
    }

    public sealed class OrderBuildingAnchor : MonoBehaviour
    {
        [SerializeField] private OrderBuildingRole role = OrderBuildingRole.Restaurant;
        [SerializeField] private string anchorId = string.Empty;
        [SerializeField] private string displayName = string.Empty;

        public OrderBuildingRole Role
        {
            get => role;
            set => role = value;
        }

        public string AnchorId
        {
            get => anchorId;
            set => anchorId = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }
    }
}
