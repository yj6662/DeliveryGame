using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SpeedFollowProxyDriver : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody targetRb;
        [SerializeField] private bool useWorldOffset = true;
        [SerializeField] private Vector3 baseWorldOffset = new Vector3(-10f, 10f, -10f);
        [SerializeField] private Vector3 baseLocalOffset = new Vector3(0f, 2.2f, -6f);
        [SerializeField] private float extraBackAtHighSpeed = 6f;
        [SerializeField] private float speedLagStart = 14f;
        [SerializeField] private float speedLagEnd = 28f;
        [SerializeField] private float followSharpnessLowSpeed = 16f;
        [SerializeField] private float followSharpnessHighSpeed = 5f;
        [SerializeField] private float yawFollowSharpness = 8f;

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
            targetRb = null;
            ResolveTargetRigidbody();
        }

        private void Awake()
        {
            ResolveTargetRigidbody();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            ResolveTargetRigidbody();

            float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
            float t = Mathf.InverseLerp(speedLagStart, speedLagEnd, speed);
            Vector3 desiredPos;
            if (useWorldOffset)
            {
                Vector3 flat = new Vector3(baseWorldOffset.x, 0f, baseWorldOffset.z);
                Vector3 backDir = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.back;
                desiredPos = target.position + baseWorldOffset + (backDir * extraBackAtHighSpeed * t);
            }
            else
            {
                Vector3 desiredLocal = baseLocalOffset + (Vector3.back * (extraBackAtHighSpeed * t));
                desiredPos = target.position + (target.rotation * desiredLocal);
            }

            float sharpness = Mathf.Lerp(followSharpnessLowSpeed, followSharpnessHighSpeed, t);
            float dt = Time.unscaledDeltaTime;
            float posSmooth = 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * dt);
            transform.position = Vector3.Lerp(transform.position, desiredPos, posSmooth);

            Vector3 targetForwardFlat = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (targetForwardFlat.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredYaw = Quaternion.LookRotation(targetForwardFlat, Vector3.up);
                float rotSmooth = 1f - Mathf.Exp(-Mathf.Max(0f, yawFollowSharpness) * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredYaw, rotSmooth);
            }
        }

        private void ResolveTargetRigidbody()
        {
            if (target == null || targetRb != null)
            {
                return;
            }

            targetRb = target.GetComponentInParent<Rigidbody>();
            if (targetRb == null)
            {
                targetRb = target.GetComponent<Rigidbody>();
            }
        }
    }
}
