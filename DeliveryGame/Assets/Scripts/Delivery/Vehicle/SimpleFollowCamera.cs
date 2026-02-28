using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SimpleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody targetRb;

        [Header("Position")]
        [SerializeField] private float heightOffset = 13f;
        [SerializeField] private float followDistance = 7f;
        [SerializeField] private float smoothTime = 0.3f;

        [Header("Rotation")]
        [SerializeField] private float pitchAngle = 35f;
        [Range(0f, 1f)]
        [SerializeField] private float yawFollowStrength = 0.45f;
        [SerializeField] private float yawSmoothTime = 0.32f;
        [SerializeField] private float yawDeadZoneDegrees = 1.1f;

        [Header("Speed Trailing")]
        [SerializeField] private float extraBackAtHighSpeed = 6f;
        [SerializeField] private float speedLagStart = 12f;
        [SerializeField] private float speedLagEnd = 28f;
        [SerializeField] private float followSharpnessLowSpeed = 8f;
        [SerializeField] private float followSharpnessHighSpeed = 2.8f;

        [Header("Occlusion")]
        [SerializeField] private bool preventClipping = true;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float occlusionPivotHeight = 1.4f;
        [SerializeField] private float collisionRadius = 0.35f;
        [SerializeField] private float collisionBuffer = 0.2f;
        [SerializeField] private float minDistanceFromTarget = 2f;
        [SerializeField] private float collisionBackoffStep = 0.35f;
        [SerializeField] private int collisionResolveSteps = 8;
        [SerializeField] private float nearClipWhenOccluded = 0.01f;
        [SerializeField] private float defaultNearClip = 0.03f;

        [Header("Occluder Fade")]
        [SerializeField] private bool fadeOccludingRenderers = true;
        [Range(0.05f, 1f)]
        [SerializeField] private float occluderFadeAlpha = 0.22f;
        [SerializeField] private int maxFadedOccluders = 12;

        [Header("Bounds Occlusion")]
        [SerializeField] private bool useRendererBoundsOcclusion = true;
        [SerializeField] private float occluderBoundsPadding = 0.2f;
        [SerializeField] private float minOccluderHeight = 1.25f;
        [SerializeField] private float rendererCacheRefreshSeconds = 1.0f;

        [Header("Clip Plane Tuning")]
        [SerializeField] private bool tuneCameraClipPlanes = true;
        [SerializeField] private float tunedFarClip = 1800f;

        [Header("Orthographic Clip Guard")]
        [SerializeField] private bool enableOrthographicBackstepGuard = true;
        [SerializeField] private float safeZ = 0.05f;
        [SerializeField] private float safeZMargin = 0.02f;
        [SerializeField] private float scanRadiusMultiplier = 1.5f;
        [SerializeField] private float checkInterval = 0.14f;
        [SerializeField] private float backstepSmoothTime = 0.32f;
        [SerializeField] private float backstepHysteresis = 0.18f;
        [SerializeField] private float backstepExpandSharpness = 6f;
        [SerializeField] private float backstepReleaseSharpness = 2f;
        [SerializeField] private float maxExtraDistance = 200f;
        [SerializeField] private bool drawDebugGizmos;

        [Header("Auto Find")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private string playerTag = "Player";

        private bool _loggedNoTarget;
        private Camera _cam;

        private CameraFollowController _follow;
        private OcclusionSystem _occlusion;
        private OccluderFadeController _fade;
        private OrthographicClipGuard _clipGuard;

        public void SetTarget(Transform nextTarget)
        {
            target = nextTarget;
            targetRb = null;
            ResolveTargetRigidbody();
            _follow.Reset();
        }

        public void RefreshTarget()
        {
            target = null;
            targetRb = null;
            _loggedNoTarget = false;
            _follow.Reset();
            TryFindTargetByTag();
        }

        public void SetTargetRigidbody(Rigidbody nextRigidbody)
        {
            targetRb = nextRigidbody;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _follow = new CameraFollowController();
            _occlusion = new OcclusionSystem();
            _fade = new OccluderFadeController();
            _clipGuard = new OrthographicClipGuard();

            ResolveTargetRigidbody();
            _occlusion.RefreshRendererCache(target, transform, BuildOcclusionParams(), force: true);
            _clipGuard.Initialize();
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

                _fade.RestoreAllFadedOccluders();
                return;
            }

            if (_loggedNoTarget)
            {
                _loggedNoTarget = false;
            }

            ResolveTargetRigidbody();
            EnsureFarClip();
            EnsureNearClip(defaultNearClip);

            var followParams = new CameraFollowController.FollowParams
            {
                HeightOffset = heightOffset,
                FollowDistance = followDistance,
                SmoothTime = smoothTime,
                PitchAngle = pitchAngle,
                YawFollowStrength = yawFollowStrength,
                YawSmoothTime = yawSmoothTime,
                YawDeadZoneDegrees = yawDeadZoneDegrees,
                ExtraBackAtHighSpeed = extraBackAtHighSpeed,
                SpeedLagStart = speedLagStart,
                SpeedLagEnd = speedLagEnd,
                FollowSharpnessLowSpeed = followSharpnessLowSpeed,
                FollowSharpnessHighSpeed = followSharpnessHighSpeed,
                OcclusionPivotHeight = occlusionPivotHeight
            };

            float dt = Time.unscaledDeltaTime;
            var result = _follow.ComputeDesiredPose(target, targetRb, transform, followParams, dt);
            Vector3 desiredPosition = result.DesiredPosition;

            var occParams = BuildOcclusionParams();

            bool occluded = _occlusion.ResolveOcclusionAdjustedPosition(
                result.Pivot, desiredPosition, target, transform, occParams, out Vector3 adjustedPosition);

            if (occluded)
            {
                desiredPosition = adjustedPosition;
                EnsureNearClip(nearClipWhenOccluded);
            }

            var clipGuardParams = new OrthographicClipGuard.ClipGuardParams
            {
                Enabled = enableOrthographicBackstepGuard,
                SafeZ = safeZ,
                SafeZMargin = safeZMargin,
                ScanRadiusMultiplier = scanRadiusMultiplier,
                CheckInterval = checkInterval,
                BackstepSmoothTime = backstepSmoothTime,
                BackstepHysteresis = backstepHysteresis,
                BackstepExpandSharpness = backstepExpandSharpness,
                BackstepReleaseSharpness = backstepReleaseSharpness,
                MaxExtraDistance = maxExtraDistance,
                OccluderBoundsPadding = occluderBoundsPadding,
                ObstacleMask = obstacleMask,
                UseRendererBoundsOcclusion = useRendererBoundsOcclusion
            };

            desiredPosition = _clipGuard.Apply(
                desiredPosition, result.CameraRotation, result.Pivot, dt,
                _cam, target, transform, _occlusion, occParams, clipGuardParams);

            float sharpness = Mathf.Lerp(followSharpnessLowSpeed, followSharpnessHighSpeed, result.SpeedT);
            transform.position = _follow.SmoothPosition(transform.position, desiredPosition, smoothTime, sharpness, dt);

            var fadeParams = new OccluderFadeController.FadeParams
            {
                FadeOccludingRenderers = fadeOccludingRenderers,
                OccluderFadeAlpha = occluderFadeAlpha,
                MaxFadedOccluders = maxFadedOccluders,
                UseRendererBoundsOcclusion = useRendererBoundsOcclusion,
                CollisionRadius = collisionRadius,
                ObstacleMask = obstacleMask
            };

            _fade.UpdateOccluderFade(result.Pivot, transform.position, target, transform, _occlusion, occParams, fadeParams);
            transform.rotation = result.CameraRotation;
        }

        private void OnDisable()
        {
            _fade?.RestoreAllFadedOccluders();
            _clipGuard?.Reset();
        }

        private void OnDestroy()
        {
            _fade?.RestoreAllFadedOccluders();
        }

        private OcclusionSystem.OcclusionParams BuildOcclusionParams()
        {
            return new OcclusionSystem.OcclusionParams
            {
                PreventClipping = preventClipping,
                ObstacleMask = obstacleMask,
                CollisionRadius = collisionRadius,
                CollisionBuffer = collisionBuffer,
                MinDistanceFromTarget = minDistanceFromTarget,
                CollisionBackoffStep = collisionBackoffStep,
                CollisionResolveSteps = collisionResolveSteps,
                UseRendererBoundsOcclusion = useRendererBoundsOcclusion,
                OccluderBoundsPadding = occluderBoundsPadding,
                MinOccluderHeight = minOccluderHeight,
                RendererCacheRefreshSeconds = rendererCacheRefreshSeconds
            };
        }

        private void EnsureNearClip(float nearClip)
        {
            if (_cam == null)
            {
                _cam = GetComponent<Camera>();
                if (_cam == null) return;
            }

            float clamped = Mathf.Clamp(nearClip, 0.003f, 0.5f);
            if (Mathf.Abs(_cam.nearClipPlane - clamped) > 0.0001f)
            {
                _cam.nearClipPlane = clamped;
            }
        }

        private void EnsureFarClip()
        {
            if (!tuneCameraClipPlanes) return;

            if (_cam == null)
            {
                _cam = GetComponent<Camera>();
                if (_cam == null) return;
            }

            float clampedFar = Mathf.Clamp(tunedFarClip, 200f, 6000f);
            if (Mathf.Abs(_cam.farClipPlane - clampedFar) > 0.1f)
            {
                _cam.farClipPlane = clampedFar;
            }
        }

        private void ResolveTargetRigidbody()
        {
            if (target == null || targetRb != null) return;

            targetRb = target.GetComponentInParent<Rigidbody>();
            if (targetRb == null)
            {
                targetRb = target.GetComponent<Rigidbody>();
            }
        }

        private void TryFindTargetByTag()
        {
            if (string.IsNullOrEmpty(playerTag)) return;

            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged == null) return;

            target = tagged.transform;
            ResolveTargetRigidbody();
            _follow?.Reset();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || _clipGuard == null) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.55f);
            Gizmos.DrawWireSphere(_clipGuard.LastScanCenter, Mathf.Max(0f, _clipGuard.LastScanRadius));
        }
    }
}
