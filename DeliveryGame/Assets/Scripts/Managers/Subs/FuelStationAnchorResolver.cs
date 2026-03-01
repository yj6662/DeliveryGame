using System;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class FuelStationAnchorResolver
    {
        private const float SpawnYOffset = 0.3f;

        private readonly ServiceRegistry _services;

        private RoadQueryManager _roadQuery;
        private MotorbikeController _player;
        private OrderBuildingAnchor _stationAnchor;
        private bool _missingAnchorLogged;
        private string _currentRegionId = string.Empty;

        internal FuelStationAnchorResolver(ServiceRegistry services)
        {
            _services = services;
        }

        internal OrderBuildingAnchor CurrentAnchor => _stationAnchor;

        internal void ResetRegionTracking()
        {
            _currentRegionId = string.Empty;
            _missingAnchorLogged = false;
        }

        internal void CleanupSceneState()
        {
            _player = null;
            _stationAnchor = null;
            _currentRegionId = string.Empty;
            _missingAnchorLogged = false;
        }

        internal void EnsureRuntimeReferences()
        {
            if (_roadQuery == null)
            {
                _services.TryGet(out _roadQuery);
            }

            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<MotorbikeController>();
            }
        }

        internal bool UpdateStationAnchorForCurrentRegion(bool forceRefresh)
        {
            bool anchorWasPresent = _stationAnchor != null;
            if (anchorWasPresent && !_stationAnchor.gameObject.activeInHierarchy)
            {
                _stationAnchor = null;
                anchorWasPresent = false;
            }

            if (_player == null)
            {
                return false;
            }

            string regionId = ResolvePlayerRegionId();
            bool regionChanged = !string.Equals(regionId, _currentRegionId, StringComparison.Ordinal);
            if (!regionChanged && !forceRefresh && _stationAnchor != null)
            {
                return false;
            }

            OrderBuildingAnchor resolved = FindGasAnchorForRegion(regionId);
            bool anchorChanged = resolved != _stationAnchor || forceRefresh || (anchorWasPresent && resolved == null);
            _currentRegionId = regionId;
            _stationAnchor = resolved;

            if (_stationAnchor == null)
            {
                if (!_missingAnchorLogged)
                {
                    _missingAnchorLogged = true;
                    Debug.LogWarning("[FuelStationRuntime] Gas station anchor not found for region: " + regionId);
                }

                return anchorChanged;
            }

            _missingAnchorLogged = false;
            return anchorChanged;
        }

        internal bool TryResolveRoadSidePosition(Vector3 reference, out Vector3 roadSidePosition)
        {
            roadSidePosition = reference;

            if (_roadQuery == null)
            {
                _services.TryGet(out _roadQuery);
            }

            if (_roadQuery != null)
            {
                _roadQuery.RefreshRoadCache();
                if (_roadQuery.GetNearestRoadPoint(reference, out Vector3 nearestRoad))
                {
                    nearestRoad.y += SpawnYOffset;
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

            if (found)
            {
                roadSidePosition.y += SpawnYOffset;
            }

            return found;
        }

        private OrderBuildingAnchor FindGasAnchorForRegion(string regionId)
        {
            OrderBuildingAnchor[] anchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsSortMode.None);
            OrderBuildingAnchor fallback = null;
            for (int i = 0; i < anchors.Length; i++)
            {
                OrderBuildingAnchor anchor = anchors[i];
                if (anchor == null || anchor.Role != OrderBuildingRole.GasStation)
                {
                    continue;
                }

                if (string.Equals(anchor.RegionId, regionId, StringComparison.Ordinal))
                {
                    return anchor;
                }

                if (fallback == null)
                {
                    fallback = anchor;
                }
            }

            return fallback;
        }

        private string ResolvePlayerRegionId()
        {
            if (_player == null)
            {
                return "central";
            }

            return RegionWorldLayout.ResolveRegionId(_player.transform.position);
        }
    }
}
