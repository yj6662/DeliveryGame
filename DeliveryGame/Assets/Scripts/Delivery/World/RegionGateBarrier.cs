using UnityEngine;

namespace DeliveryRun.Delivery.World
{
    [DisallowMultipleComponent]
    public sealed class RegionGateBarrier : MonoBehaviour
    {
        [SerializeField] private string regionId = string.Empty;
        [SerializeField] private Collider blockerCollider;
        [SerializeField] private Renderer[] barrierRenderers;
        [SerializeField] private bool locked = true;

        public string RegionId
        {
            get => regionId;
            set => regionId = value;
        }

        public bool IsLocked => locked;

        private void Awake()
        {
            EnsureReferences();
            ApplyLockedState(locked);
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        public void ConfigureForEditor(string targetRegionId, Collider colliderRef, Renderer[] rendererRefs)
        {
            regionId = targetRegionId;
            blockerCollider = colliderRef;
            barrierRenderers = rendererRefs;
            EnsureReferences();
        }

        public void ApplyLockedState(bool isLocked)
        {
            locked = isLocked;

            if (blockerCollider != null)
            {
                blockerCollider.enabled = isLocked;
            }

            if (barrierRenderers == null)
            {
                return;
            }

            for (int i = 0; i < barrierRenderers.Length; i++)
            {
                Renderer renderer = barrierRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = isLocked;
            }
        }

        private void EnsureReferences()
        {
            if (blockerCollider == null)
            {
                blockerCollider = GetComponent<Collider>();
            }

            if (barrierRenderers == null || barrierRenderers.Length == 0)
            {
                barrierRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }
    }
}
