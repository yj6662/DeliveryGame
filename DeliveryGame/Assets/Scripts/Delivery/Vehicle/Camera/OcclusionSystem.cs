using UnityEngine;
using System.Collections.Generic;

namespace DeliveryRun.Delivery.Vehicle
{
    /// <summary>
    /// Collision detection, bounds intersection testing, and renderer cache
    /// for camera occlusion resolution.
    /// </summary>
    internal sealed class OcclusionSystem
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

        private bool TryGetNearestBoundsHit(
            Vector3 origin, Vector3 direction, float maxDistance,
            Transform target, Transform cameraTransform,
            OcclusionParams p, out BoundsHit nearestHit)
        {
            nearestHit = default;
            if (maxDistance <= Epsilon) return false;

            RefreshRendererCache(target, cameraTransform, p);
            _boundsHits.Clear();

            for (int i = 0; i < _occluderRenderers.Count; i++)
            {
                Renderer renderer = _occluderRenderers[i];
                if (!IsRendererUsable(renderer, target, cameraTransform, p))
                    continue;
                if (!IsLayerIncluded(renderer.gameObject.layer, p.ObstacleMask))
                    continue;

                Bounds bounds = renderer.bounds;
                if (target != null && bounds.max.y < target.position.y + Mathf.Max(0.5f, p.MinOccluderHeight))
                    continue;

                bounds.Expand(Mathf.Max(0f, p.OccluderBoundsPadding) * 2f);
                if (!TrySegmentBoundsIntersection(origin, direction, maxDistance, bounds, out float enterDistance))
                    continue;

                _boundsHits.Add(new BoundsHit { Renderer = renderer, Distance = enterDistance });
            }

            if (_boundsHits.Count == 0) return false;

            _boundsHits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            nearestHit = _boundsHits[0];
            return true;
        }

        internal bool IsInsideBoundsOccluder(Vector3 position, Transform target, Transform cameraTransform, OcclusionParams p)
        {
            RefreshRendererCache(target, cameraTransform, p);
            for (int i = 0; i < _occluderRenderers.Count; i++)
            {
                Renderer renderer = _occluderRenderers[i];
                if (!IsRendererUsable(renderer, target, cameraTransform, p))
                    continue;
                if (!IsLayerIncluded(renderer.gameObject.layer, p.ObstacleMask))
                    continue;

                Bounds bounds = renderer.bounds;
                if (target != null && bounds.max.y < target.position.y + Mathf.Max(0.5f, p.MinOccluderHeight))
                    continue;

                bounds.Expand(Mathf.Max(0f, p.OccluderBoundsPadding) * 2f);
                if (bounds.Contains(position))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Collects bounds occluders between origin and camera for fade purposes.
        /// Returns true if any were found. Results are in BoundsHits.
        /// </summary>
        internal bool CollectBoundsOccluders(
            Vector3 origin, Vector3 direction, float maxDistance,
            Transform target, Transform cameraTransform, OcclusionParams p)
        {
            RefreshRendererCache(target, cameraTransform, p);
            _boundsHits.Clear();

            for (int i = 0; i < _occluderRenderers.Count; i++)
            {
                Renderer renderer = _occluderRenderers[i];
                if (!IsRendererUsable(renderer, target, cameraTransform, p))
                    continue;
                if (!IsLayerIncluded(renderer.gameObject.layer, p.ObstacleMask))
                    continue;

                Bounds bounds = renderer.bounds;
                if (target != null && bounds.max.y < target.position.y + Mathf.Max(0.5f, p.MinOccluderHeight))
                    continue;

                bounds.Expand(Mathf.Max(0f, p.OccluderBoundsPadding) * 2f);
                if (!TrySegmentBoundsIntersection(origin, direction, maxDistance, bounds, out float enterDistance))
                    continue;

                _boundsHits.Add(new BoundsHit { Renderer = renderer, Distance = enterDistance });
            }

            if (_boundsHits.Count == 0) return false;

            _boundsHits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return true;
        }

        internal void RefreshRendererCache(Transform target, Transform cameraTransform, OcclusionParams p, bool force = false)
        {
            if (!p.UseRendererBoundsOcclusion) return;

            float now = Time.unscaledTime;
            if (!force && now < _nextRendererCacheRefreshTime && _occluderRenderers.Count > 0)
                return;

            _nextRendererCacheRefreshTime = now + Mathf.Max(0.2f, p.RendererCacheRefreshSeconds);
            _occluderRenderers.Clear();

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsRendererUsable(renderer, target, cameraTransform, p))
                    continue;
                if (!IsLayerIncluded(renderer.gameObject.layer, p.ObstacleMask))
                    continue;

                _occluderRenderers.Add(renderer);
            }
        }

        internal static bool IsRendererUsable(Renderer renderer, Transform target, Transform cameraTransform, OcclusionParams p)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                return false;

            Transform tr = renderer.transform;
            if (target != null && (tr == target || tr.IsChildOf(target) || target.IsChildOf(tr)))
                return false;
            if (cameraTransform != null && (tr == cameraTransform || tr.IsChildOf(cameraTransform) || cameraTransform.IsChildOf(tr)))
                return false;

            Bounds bounds = renderer.bounds;
            if (bounds.size.y < Mathf.Max(0.2f, p.MinOccluderHeight * 0.5f))
                return false;

            return true;
        }

        internal static bool IsLayerIncluded(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        internal static bool TrySegmentBoundsIntersection(Vector3 origin, Vector3 direction, float maxDistance, Bounds bounds, out float enterDistance)
        {
            enterDistance = 0f;
            float tMin = 0f;
            float tMax = maxDistance;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            if (!ClipAxis(origin.x, direction.x, min.x, max.x, ref tMin, ref tMax) ||
                !ClipAxis(origin.y, direction.y, min.y, max.y, ref tMin, ref tMax) ||
                !ClipAxis(origin.z, direction.z, min.z, max.z, ref tMin, ref tMax))
            {
                return false;
            }

            enterDistance = Mathf.Max(0f, tMin);
            return enterDistance <= maxDistance;
        }

        private static bool ClipAxis(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(direction) < Epsilon)
            {
                return origin >= min && origin <= max;
            }

            float inv = 1f / direction;
            float t1 = (min - origin) * inv;
            float t2 = (max - origin) * inv;
            if (t1 > t2) (t1, t2) = (t2, t1);

            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;

            return tMin <= tMax;
        }
    }
}
