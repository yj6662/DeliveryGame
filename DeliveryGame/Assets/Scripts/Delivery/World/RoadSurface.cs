using UnityEngine;

namespace DeliveryRun.Delivery.World
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class RoadSurface : MonoBehaviour
    {
        [SerializeField] private Collider cachedCollider;

        public Collider CachedCollider => cachedCollider;

        private void Awake()
        {
            EnsureCollider();
        }

        private void OnValidate()
        {
            EnsureCollider();
        }

        private void EnsureCollider()
        {
            if (cachedCollider != null)
            {
                return;
            }

            cachedCollider = GetComponent<Collider>();
        }
    }
}
