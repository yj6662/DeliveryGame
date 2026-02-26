using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SimpleFollowCamera : MonoBehaviour
    {
        private const int OverlapBufferSize = 24;

        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody targetRb;

        [Header("Position")]
        [SerializeField] private float heightOffset = 10f;
        [SerializeField] private float followDistance = 7f;
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Rotation")]
        [SerializeField] private float pitchAngle = 35f;
        [Range(0f, 1f)]
        [SerializeField] private float yawFollowStrength = 0.9f;
        [SerializeField] private float yawSmoothTime = 0.12f;

        [Header("Speed Trailing")]
        [SerializeField] private float extraBackAtHighSpeed = 6f;
        [SerializeField] private float speedLagStart = 12f;
        [SerializeField] private float speedLagEnd = 28f;
        [SerializeField] private float followSharpnessLowSpeed = 14f;
        [SerializeField] private float followSharpnessHighSpeed = 5f;

        [Header("Occlusion")]
        [SerializeField] private bool preventClipping = true;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float occlusionPivotHeight = 1.4f;
        [SerializeField] private float collisionRadius = 0.35f;
        [SerializeField] private float collisionBuffer = 0.2f;
        [SerializeField] private float minDistanceFromTarget = 2f;
        [SerializeField] private float collisionBackoffStep = 0.35f;
        [SerializeField] private int collisionResolveSteps = 8;
        [SerializeField] private float nearClipWhenOccluded = 0f;
        [SerializeField] private float defaultNearClip = 0f;

        [Header("Auto Find")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private string playerTag = "Player";

        private Vector3 _positionVelocity;
        private float _currentYaw;
        private float _yawVelocity;
        private bool _initialized;
        private bool _loggedNoTarget;
        private Camera _cam;
        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
            targetRb = null;
            ResolveTargetRigidbody();
            _initialized = false;
        }

        public void RefreshTarget()
        {
            target = null;
            targetRb = null;
            _initialized = false;
            _loggedNoTarget = false;
            TryFindTargetByTag();
        }

        public void SetTargetRigidbody(Rigidbody nextRigidbody)
        {
            targetRb = nextRigidbody;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            ResolveTargetRigidbody();
        }

        private void LateUpdate()
        {
            if (target == null && autoFindPlayer)
            {
                TryFindTargetByTag();
            }

            if (target == null)
            {
                if (!_loggedNoTarget)
                {
                    Debug.LogWarning("[SimpleFollowCamera] Target is missing. Camera follow paused.");
                    _loggedNoTarget = true;
                }

                return;
            }

            if (_loggedNoTarget)
            {
                _loggedNoTarget = false;
            }

            ResolveTargetRigidbody();
            EnsureNearClip(defaultNearClip);

            float targetYaw = target.eulerAngles.y;
            if (!_initialized)
            {
                _currentYaw = targetYaw;
                _initialized = true;
            }

            float yawDelta = Mathf.DeltaAngle(_currentYaw, targetYaw);
            float desiredYaw = _currentYaw + (yawDelta * Mathf.Clamp01(yawFollowStrength));
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, desiredYaw, ref _yawVelocity, Mathf.Max(0.01f, yawSmoothTime));

            float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
            float speedT = Mathf.InverseLerp(speedLagStart, speedLagEnd, speed);
            float followDistanceWithLag = followDistance + (extraBackAtHighSpeed * speedT);

            Quaternion yawRotation = Quaternion.Euler(0f, _currentYaw, 0f);
            Vector3 back = yawRotation * Vector3.back;
            Vector3 pivot = target.position + (Vector3.up * Mathf.Max(0.2f, occlusionPivotHeight));
            Vector3 desiredPosition = target.position + (back * followDistanceWithLag) + (Vector3.up * heightOffset);
            desiredPosition = ResolveOcclusionAdjustedPosition(pivot, desiredPosition);

            float sharpness = Mathf.Lerp(followSharpnessLowSpeed, followSharpnessHighSpeed, speedT);
            float dt = Time.unscaledDeltaTime;
            float expT = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * dt);
            if (smoothTime > 0.0001f)
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, smoothTime, Mathf.Infinity, dt);
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, expT);
            }

            transform.rotation = Quaternion.Euler(pitchAngle, _currentYaw, 0f);
        }

        private Vector3 ResolveOcclusionAdjustedPosition(Vector3 pivot, Vector3 desiredPosition)
        {
            if (!preventClipping || target == null)
            {
                return desiredPosition;
            }

            Vector3 toDesired = desiredPosition - pivot;
            float desiredDistance = toDesired.magnitude;
            if (desiredDistance <= 0.0001f)
            {
                return desiredPosition;
            }

            Vector3 direction = toDesired / desiredDistance;
            float castDistance = Mathf.Max(0f, desiredDistance - 0.01f);
            if (castDistance <= 0.0001f)
            {
                return desiredPosition;
            }

            if (!Physics.SphereCast(
                    pivot,
                    Mathf.Max(0.02f, collisionRadius),
                    direction,
                    out RaycastHit hit,
                    castDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                EnsureNearClip(defaultNearClip);
                return desiredPosition;
            }

            if (hit.transform != null && target != null && hit.transform.IsChildOf(target))
            {
                EnsureNearClip(defaultNearClip);
                return desiredPosition;
            }

            float minDistance = Mathf.Max(0.5f, minDistanceFromTarget);
            float safeDistance = Mathf.Max(minDistance, hit.distance - Mathf.Max(0f, collisionBuffer));
            Vector3 occluded = pivot + (direction * safeDistance);
            EnsureNearClip(nearClipWhenOccluded);
            return ResolvePenetrationByBackingOff(pivot, direction, occluded, minDistance);
        }

        private Vector3 ResolvePenetrationByBackingOff(Vector3 pivot, Vector3 direction, Vector3 startPosition, float minDistance)
        {
            if (!IsPositionBlocked(startPosition))
            {
                return startPosition;
            }

            float startDistance = Vector3.Distance(pivot, startPosition);
            float step = Mathf.Max(0.05f, collisionBackoffStep);
            int steps = Mathf.Max(1, collisionResolveSteps);

            for (int i = 0; i < steps; i++)
            {
                startDistance -= step;
                if (startDistance <= minDistance)
                {
                    break;
                }

                Vector3 candidate = pivot + (direction * startDistance);
                if (!IsPositionBlocked(candidate))
                {
                    return candidate;
                }
            }

            return pivot + (direction * minDistance);
        }

        private bool IsPositionBlocked(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(
                position,
                Mathf.Max(0.02f, collisionRadius),
                _overlapBuffer,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                Transform colTr = col.transform;
                if (target != null && (colTr == target || colTr.IsChildOf(target) || target.IsChildOf(colTr)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void EnsureNearClip(float nearClip)
        {
            if (_cam == null)
            {
                _cam = GetComponent<Camera>();
                if (_cam == null)
                {
                    return;
                }
            }

            float clamped = Mathf.Clamp(nearClip, 0f, 0.5f);
            if (Mathf.Abs(_cam.nearClipPlane - clamped) > 0.0001f)
            {
                _cam.nearClipPlane = clamped;
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

        private void TryFindTargetByTag()
        {
            if (string.IsNullOrEmpty(playerTag))
            {
                return;
            }

            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged == null)
            {
                return;
            }

            target = tagged.transform;
            ResolveTargetRigidbody();
            _initialized = false;
        }
    }
}
