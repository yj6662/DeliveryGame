using UnityEngine;

namespace DeliveryRun.Delivery.World
{
    [DisallowMultipleComponent]
    public sealed class SectorThemeMarker : MonoBehaviour
    {
        [SerializeField] private string regionId = "central";

        public string RegionId
        {
            get => regionId;
            set => regionId = string.IsNullOrEmpty(value) ? "central" : value;
        }
    }
}
