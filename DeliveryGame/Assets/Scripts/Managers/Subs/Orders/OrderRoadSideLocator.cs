using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderRoadSideLocator
    {
        internal bool TryResolve(OrderBuildingAnchor anchor, RoadQueryManager roadQuery, out Vector3 roadSidePosition)
        {
            roadSidePosition = Vector3.zero;
            if (anchor == null)
            {
                return false;
            }

            Vector3 reference = anchor.transform.position;
            if (roadQuery != null)
            {
                roadQuery.RefreshRoadCache();
                if (roadQuery.GetNearestRoadPoint(reference, out Vector3 nearestRoad))
                {
                    nearestRoad.y += 0.45f;
                    roadSidePosition = nearestRoad;
                    return true;
                }
            }

            RoadSurface[] surfaces = Object.FindObjectsByType<RoadSurface>(FindObjectsSortMode.None);
            float bestSqrDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < surfaces.Length; i++)
            {
                RoadSurface surface = surfaces[i];
                if (surface == null)
                {
                    continue;
                }

                Collider collider = surface.CachedCollider;
                if (collider == null)
                {
                    collider = surface.GetComponent<Collider>();
                }

                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 candidate = collider.ClosestPoint(reference);
                float sqrDistance = (candidate - reference).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                roadSidePosition = candidate;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            roadSidePosition.y += 0.45f;
            return true;
        }
    }
}
