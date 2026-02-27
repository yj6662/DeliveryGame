using UnityEngine;

namespace DeliveryRun.Delivery.Lobby
{
    [DisallowMultipleComponent]
    public sealed class LobbyCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 worldOffset = new Vector3(-9.5f, 10f, -9.5f);
        [SerializeField] private float positionSharpness = 10f;
        [SerializeField] private float rotationSharpness = 11f;
        [SerializeField] private float lookAhead = 0.4f;
        [SerializeField] private float lookHeight = 1.3f;
        [SerializeField] private bool keepQuarterRotation = true;
        [SerializeField] private Vector3 quarterEuler = new Vector3(35f, 45f, 0f);

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 desiredPos = target.position + worldOffset;
            float posT = 1f - Mathf.Exp(-Mathf.Max(0.1f, positionSharpness) * dt);
            transform.position = Vector3.Lerp(transform.position, desiredPos, posT);

            Quaternion desiredRot;
            if (keepQuarterRotation)
            {
                desiredRot = Quaternion.Euler(quarterEuler);
            }
            else
            {
                Vector3 lookPoint = target.position + (target.forward * lookAhead) + (Vector3.up * lookHeight);
                Vector3 lookDir = lookPoint - transform.position;
                if (lookDir.sqrMagnitude <= 0.0001f)
                {
                    return;
                }

                desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            float rotT = 1f - Mathf.Exp(-Mathf.Max(0.1f, rotationSharpness) * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotT);
        }
    }
}
