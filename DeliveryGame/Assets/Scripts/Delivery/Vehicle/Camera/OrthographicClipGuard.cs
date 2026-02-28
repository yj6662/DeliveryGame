using UnityEngine;
using System.Collections.Generic;

namespace DeliveryRun.Delivery.Vehicle
{
    /// <summary>
    /// Prevents frustum clipping in orthographic cameras by computing a backstep distance
    /// that ensures all nearby geometry stays in front of the near clip plane.
    /// </summary>
    internal sealed class OrthographicClipGuard
    {
        private const int ClipGuardOverlapBufferSize = 256;
        private const float Epsilon = 0.0001f;

        private static readonly Vector3[] BoundsCornerSigns =
        {
            new Vector3(-1f, -1f, -1f),
            new Vector3(-1f, -1f, 1f),
            new Vector3(-1f, 1f, -1f),
            new Vector3(-1f, 1f, 1f),
            new Vector3(1f, -1f, -1f),
            new Vector3(1f, -1f, 1f),
            new Vector3(1f, 1f, -1f),
            new Vector3(1f, 1f, 1f)
        };

        private readonly Collider[] _clipGuardOverlapBuffer = new Collider[ClipGuardOverlapBufferSize];
        private readonly HashSet<Renderer> _clipGuardRendererSet = new HashSet<Renderer>();
        private float _backstepDistance;
        private float _backstepVelocity;
        private float _backstepTargetDistance;
        private float _nextClipGuardCheckTime;

        // Debug state exposed for gizmos
        internal Vector3 LastScanCenter { get; private set; }
        internal float LastScanRadius { get; private set; }
        internal float LastScanMinZ { get; private set; } = float.PositiveInfinity;

        public struct ClipGuardParams
        {
            public bool Enabled;
            public float SafeZ;
            public float SafeZMargin;
            public float ScanRadiusMultiplier;
            public float CheckInterval;
            public float BackstepSmoothTime;
            public float BackstepHysteresis;
            public float BackstepExpandSharpness;
            public float BackstepReleaseSharpness;
            public float MaxExtraDistance;
            public float OccluderBoundsPadding;
            public LayerMask ObstacleMask;
            public bool UseRendererBoundsOcclusion;
        }

        public void Reset()
        {
            _backstepTargetDistance = 0f;
            _backstepDistance = 0f;
            _backstepVelocity = 0f;
        }

        public void Initialize()
        {
            _nextClipGuardCheckTime = Time.unscaledTime;
            LastScanMinZ = float.PositiveInfinity;
        }

        public Vector3 Apply(
            Vector3 desiredPosition,
            Quaternion cameraRotation,
            Vector3 scanPivot,
            float deltaTime,
            Camera cam,
            Transform target,
            Transform cameraTransform,
            OcclusionSystem occlusion,
            OcclusionSystem.OcclusionParams occParams,
            ClipGuardParams p)
        {
            if (!p.Enabled || target == null)
            {
                Reset();
                return desiredPosition;
            }

            if (cam == null || !cam.orthographic)
            {
                Reset();
                return desiredPosition;
            }

            float now = Time.unscaledTime;
            if (now >= _nextClipGuardCheckTime)
            {
                float interval = Mathf.Max(0.02f, p.CheckInterval);
                _nextClipGuardCheckTime = now + interval;
                float computedDistance = ComputeRequiredBackstepDistance(
                    desiredPosition, cameraRotation, scanPivot, cam, target, cameraTransform, occlusion, occParams, p);
                computedDistance = Mathf.Clamp(computedDistance, 0f, Mathf.Max(0f, p.MaxExtraDistance));

                if (Mathf.Abs(computedDistance - _backstepTargetDistance) <= Mathf.Max(0f, p.BackstepHysteresis))
                {
                    computedDistance = _backstepTargetDistance;
                }

                float sharpness = computedDistance > _backstepTargetDistance
                    ? Mathf.Max(0f, p.BackstepExpandSharpness)
                    : Mathf.Max(0f, p.BackstepReleaseSharpness);

                if (sharpness <= Epsilon)
                {
                    _backstepTargetDistance = computedDistance;
                }
                else
                {
                    float targetBlend = 1f - Mathf.Exp(-sharpness * interval);
                    _backstepTargetDistance = Mathf.Lerp(_backstepTargetDistance, computedDistance, targetBlend);
                }
            }

            float dampTime = Mathf.Max(0.01f, p.BackstepSmoothTime);
            float dt = Mathf.Max(0.0001f, deltaTime);
            _backstepDistance = Mathf.SmoothDamp(
                _backstepDistance,
                _backstepTargetDistance,
                ref _backstepVelocity,
                dampTime,
                Mathf.Infinity,
                dt);

            float extraDistance = Mathf.Clamp(_backstepDistance, 0f, Mathf.Max(0f, p.MaxExtraDistance));
            return desiredPosition - ((cameraRotation * Vector3.forward) * extraDistance);
        }

        private float ComputeRequiredBackstepDistance(
            Vector3 cameraPosition, Quaternion cameraRotation, Vector3 scanPivot,
            Camera cam, Transform target, Transform cameraTransform,
            OcclusionSystem occlusion, OcclusionSystem.OcclusionParams occParams,
            ClipGuardParams p)
        {
            float threshold = Mathf.Max(
                Mathf.Max(0.001f, p.SafeZ),
                cam.nearClipPlane + Mathf.Max(0f, p.SafeZMargin));

            float orthoSize = Mathf.Max(0.5f, cam.orthographicSize);
            float orthoHalfWidth = orthoSize * Mathf.Max(1f, cam.aspect);
            float scanRadius = Mathf.Max(orthoSize, orthoHalfWidth) * Mathf.Max(0.1f, p.ScanRadiusMultiplier);
            scanRadius = Mathf.Clamp(scanRadius, 2f, 2000f);

            Vector3 scanCenter = scanPivot;
            LastScanCenter = scanCenter;
            LastScanRadius = scanRadius;
            LastScanMinZ = float.PositiveInfinity;

            _clipGuardRendererSet.Clear();
            int overlapCount = Physics.OverlapSphereNonAlloc(
                scanCenter,
                scanRadius,
                _clipGuardOverlapBuffer,
                p.ObstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = _clipGuardOverlapBuffer[i];
                if (col == null || !col.enabled) continue;

                Transform tr = col.transform;
                if (IsTargetOrCameraHierarchy(tr, target, cameraTransform)) continue;

                Renderer renderer = col.GetComponentInParent<Renderer>();
                if (!OcclusionSystem.IsRendererUsable(renderer, target, cameraTransform, occParams)) continue;

                _clipGuardRendererSet.Add(renderer);
            }

            if (p.UseRendererBoundsOcclusion)
            {
                occlusion.RefreshRendererCache(target, cameraTransform, occParams);
                float scanSqr = scanRadius * scanRadius;
                var renderers = occlusion.OccluderRenderers;
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i];
                    if (!OcclusionSystem.IsRendererUsable(renderer, target, cameraTransform, occParams)) continue;

                    Bounds bounds = renderer.bounds;
                    Vector3 nearest = bounds.ClosestPoint(scanCenter);
                    if ((nearest - scanCenter).sqrMagnitude > scanSqr) continue;

                    _clipGuardRendererSet.Add(renderer);
                }
            }

            if (_clipGuardRendererSet.Count == 0) return 0f;

            Vector3 camForward = cameraRotation * Vector3.forward;
            foreach (Renderer renderer in _clipGuardRendererSet)
            {
                if (renderer == null) continue;

                Bounds bounds = renderer.bounds;
                bounds.Expand(Mathf.Max(0f, p.OccluderBoundsPadding) * 2f);

                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                for (int i = 0; i < BoundsCornerSigns.Length; i++)
                {
                    Vector3 sign = BoundsCornerSigns[i];
                    Vector3 corner = center + Vector3.Scale(extents, sign);
                    float z = Vector3.Dot(corner - cameraPosition, camForward);
                    if (z < LastScanMinZ) LastScanMinZ = z;
                }
            }

            if (float.IsPositiveInfinity(LastScanMinZ)) return 0f;

            float needed = threshold - LastScanMinZ;
            return Mathf.Clamp(needed, 0f, Mathf.Max(0f, p.MaxExtraDistance));
        }

        private static bool IsTargetOrCameraHierarchy(Transform tr, Transform target, Transform cameraTransform)
        {
            if (tr == null) return false;
            if (target != null && (tr == target || tr.IsChildOf(target) || target.IsChildOf(tr))) return true;
            if (cameraTransform != null && (tr == cameraTransform || tr.IsChildOf(cameraTransform) || cameraTransform.IsChildOf(tr))) return true;
            return false;
        }
    }
}
