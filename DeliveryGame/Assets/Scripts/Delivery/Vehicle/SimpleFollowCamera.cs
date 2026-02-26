using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SimpleFollowCamera : MonoBehaviour
    {
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

        [Header("Auto Find")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private string playerTag = "Player";

        private Vector3 _positionVelocity;
        private float _currentYaw;
        private float _yawVelocity;
        private bool _initialized;
        private bool _loggedNoTarget;

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
            Vector3 desiredPosition = target.position + (back * followDistanceWithLag) + (Vector3.up * heightOffset);

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
