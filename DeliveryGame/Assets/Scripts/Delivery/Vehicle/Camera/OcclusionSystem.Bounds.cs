using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    internal sealed partial class OcclusionSystem
    {
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
