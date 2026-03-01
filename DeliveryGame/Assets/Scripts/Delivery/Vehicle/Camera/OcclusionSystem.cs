using UnityEngine;
using System.Collections.Generic;

namespace DeliveryRun.Delivery.Vehicle
{
    /// <summary>
    /// Collision detection, bounds intersection testing, and renderer cache
    /// for camera occlusion resolution.
    /// </summary>
    internal sealed partial class OcclusionSystem
    {
        private const int OverlapBufferSize = 24;
        private const float Epsilon = 0.0001f;

        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
        private float _nextRendererCacheRefreshTime;
        private readonly List<Renderer> _occluderRenderers = new List<Renderer>(1024);
        private readonly List<BoundsHit> _boundsHits = new List<BoundsHit>(64);

        internal struct BoundsHit
        {
            public Renderer Renderer;
            public float Distance;
        }

        internal List<Renderer> OccluderRenderers => _occluderRenderers;
        internal List<BoundsHit> BoundsHits => _boundsHits;

        public struct OcclusionParams
        {
            public bool PreventClipping;
            public LayerMask ObstacleMask;
            public float CollisionRadius;
            public float CollisionBuffer;
            public float MinDistanceFromTarget;
            public float CollisionBackoffStep;
            public int CollisionResolveSteps;
            public bool UseRendererBoundsOcclusion;
            public float OccluderBoundsPadding;
            public float MinOccluderHeight;
            public float RendererCacheRefreshSeconds;
        }

        /// <summary>
        /// Resolves camera position to avoid occluding objects between pivot and desired position.
        /// Returns true if occlusion was detected (caller may want to adjust near clip).
        /// </summary>
        public bool ResolveOcclusionAdjustedPosition(
            Vector3 pivot,
            Vector3 desiredPosition,
            Transform target,
            Transform cameraTransform,
            OcclusionParams p,
            out Vector3 result)
        {
            result = desiredPosition;
            if (!p.PreventClipping || target == null)
            {
                return false;
            }

            Vector3 toDesired = desiredPosition - pivot;
            float desiredDistance = toDesired.magnitude;
            if (desiredDistance <= Epsilon)
            {
                return false;
            }

            Vector3 direction = toDesired / desiredDistance;
            float castDistance = Mathf.Max(0f, desiredDistance - 0.01f);
            if (castDistance <= Epsilon)
            {
                return false;
            }

            bool hasHit = false;
            float nearestHitDistance = float.PositiveInfinity;

            bool physicsHit = Physics.SphereCast(
                pivot,
                Mathf.Max(0.02f, p.CollisionRadius),
                direction,
                out RaycastHit hit,
                castDistance,
                p.ObstacleMask,
                QueryTriggerInteraction.Ignore);

            if (physicsHit)
            {
                bool hitTarget = hit.transform != null && target != null && hit.transform.IsChildOf(target);
                if (!hitTarget)
                {
                    nearestHitDistance = hit.distance;
                    hasHit = true;
                }
            }

            if (p.UseRendererBoundsOcclusion && TryGetNearestBoundsHit(pivot, direction, castDistance, target, cameraTransform, p, out BoundsHit boundsHit))
            {
                if (boundsHit.Distance < nearestHitDistance)
                {
                    nearestHitDistance = boundsHit.Distance;
                }
                hasHit = true;
            }

            if (!hasHit)
            {
                return false;
            }

            float minDistance = Mathf.Max(0.5f, p.MinDistanceFromTarget);
            float safeDistance = Mathf.Max(minDistance, nearestHitDistance - Mathf.Max(0f, p.CollisionBuffer));
            Vector3 occluded = pivot + (direction * safeDistance);
            result = ResolvePenetrationByBackingOff(pivot, direction, occluded, minDistance, target, cameraTransform, p);
            return true;
        }

        private Vector3 ResolvePenetrationByBackingOff(
            Vector3 pivot, Vector3 direction, Vector3 startPosition, float minDistance,
            Transform target, Transform cameraTransform, OcclusionParams p)
        {
            if (!IsPositionBlocked(startPosition, target, cameraTransform, p))
            {
                return startPosition;
            }

            float startDistance = Vector3.Distance(pivot, startPosition);
            float step = Mathf.Max(0.05f, p.CollisionBackoffStep);
            int steps = Mathf.Max(1, p.CollisionResolveSteps);

            for (int i = 0; i < steps; i++)
            {
                startDistance -= step;
                if (startDistance <= minDistance)
                {
                    break;
                }

                Vector3 candidate = pivot + (direction * startDistance);
                if (!IsPositionBlocked(candidate, target, cameraTransform, p))
                {
                    return candidate;
                }
            }

            return pivot + (direction * minDistance);
        }

        private bool IsPositionBlocked(Vector3 position, Transform target, Transform cameraTransform, OcclusionParams p)
        {
            if (p.UseRendererBoundsOcclusion && IsInsideBoundsOccluder(position, target, cameraTransform, p))
            {
                return true;
            }

            int count = Physics.OverlapSphereNonAlloc(
                position,
                Mathf.Max(0.02f, p.CollisionRadius),
                _overlapBuffer,
                p.ObstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null) continue;

                Transform colTr = col.transform;
                if (target != null && (colTr == target || colTr.IsChildOf(target) || target.IsChildOf(colTr)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

    }
}
