using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.World;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderBuildingRoleAssigner
    {
        private readonly List<Transform> _candidates = new List<Transform>(256);
        private readonly HashSet<string> _gasRegions = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _gasIndexByRegion = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _regionOrderByRegion = new Dictionary<string, int>(StringComparer.Ordinal);

        internal void EnsureAssigned()
        {
            GameObject buildingsRootObject = GameObject.Find("CityRoot/BuildingsRoot");
            Transform buildingsRoot = buildingsRootObject != null ? buildingsRootObject.transform : null;
            if (buildingsRoot == null)
            {
                return;
            }

            CollectBuildingCandidates(buildingsRoot, _candidates);
            if (_candidates.Count <= 0)
            {
                return;
            }

            int restaurantCount = 0;
            int destinationCount = 0;
            int anchoredCount = 0;
            _gasRegions.Clear();

            for (int i = 0; i < _candidates.Count; i++)
            {
                Transform child = _candidates[i];
                if (child == null)
                {
                    continue;
                }

                string resolvedRegionId = ResolveRegionIdForPosition(child.position);
                OrderBuildingAnchor anchor = child.GetComponent<OrderBuildingAnchor>();
                if (anchor == null)
                {
                    continue;
                }

                if (!string.Equals(anchor.RegionId, resolvedRegionId, StringComparison.Ordinal))
                {
                    anchor.RegionId = resolvedRegionId;
                }

                anchoredCount++;
                if (anchor.Role == OrderBuildingRole.Restaurant)
                {
                    restaurantCount++;
                }
                else if (anchor.Role == OrderBuildingRole.Destination)
                {
                    destinationCount++;
                }
                else if (anchor.Role == OrderBuildingRole.GasStation)
                {
                    _gasRegions.Add(resolvedRegionId);
                }
            }

            int requiredGasRegions = RegionWorldLayout.Count;
            if (anchoredCount == _candidates.Count &&
                restaurantCount > 0 &&
                destinationCount > 0 &&
                _gasRegions.Count >= requiredGasRegions)
            {
                return;
            }

            _gasIndexByRegion.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                Transform child = _candidates[i];
                if (child == null)
                {
                    continue;
                }

                string regionId = ResolveRegionIdForPosition(child.position);
                if (_gasIndexByRegion.ContainsKey(regionId))
                {
                    continue;
                }

                if (child.name.ToLowerInvariant().Contains("gas"))
                {
                    _gasIndexByRegion[regionId] = i;
                }
            }

            for (int i = 0; i < _candidates.Count; i++)
            {
                Transform child = _candidates[i];
                if (child == null)
                {
                    continue;
                }

                string regionId = ResolveRegionIdForPosition(child.position);
                if (!_gasIndexByRegion.ContainsKey(regionId))
                {
                    _gasIndexByRegion[regionId] = i;
                }
            }

            int restaurantSerial = 1;
            int destinationSerial = 1;
            _regionOrderByRegion.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                Transform child = _candidates[i];
                if (child == null)
                {
                    continue;
                }

                string regionId = ResolveRegionIdForPosition(child.position);
                OrderBuildingAnchor anchor = child.GetComponent<OrderBuildingAnchor>();
                if (anchor == null)
                {
                    anchor = child.gameObject.AddComponent<OrderBuildingAnchor>();
                }

                anchor.RegionId = regionId;
                if (_gasIndexByRegion.TryGetValue(regionId, out int gasIndex) && gasIndex == i)
                {
                    anchor.Role = OrderBuildingRole.GasStation;
                    anchor.AnchorId = "G_" + regionId;
                    if (string.IsNullOrEmpty(anchor.DisplayName) ||
                        !anchor.DisplayName.ToLowerInvariant().Contains("gas"))
                    {
                        anchor.DisplayName = "Gas Station (" + regionId + ")";
                    }

                    continue;
                }

                int localOrder = 0;
                if (_regionOrderByRegion.TryGetValue(regionId, out localOrder))
                {
                    _regionOrderByRegion[regionId] = localOrder + 1;
                }
                else
                {
                    localOrder = 0;
                    _regionOrderByRegion[regionId] = 1;
                }

                if ((localOrder % 4) == 0)
                {
                    anchor.Role = OrderBuildingRole.Restaurant;
                    anchor.AnchorId = "R" + restaurantSerial;
                    if (string.IsNullOrEmpty(anchor.DisplayName))
                    {
                        anchor.DisplayName = "Restaurant " + restaurantSerial + " (" + regionId + ")";
                    }

                    restaurantSerial++;
                }
                else
                {
                    anchor.Role = OrderBuildingRole.Destination;
                    anchor.AnchorId = "D" + destinationSerial;
                    if (string.IsNullOrEmpty(anchor.DisplayName))
                    {
                        anchor.DisplayName = "Destination " + destinationSerial + " (" + regionId + ")";
                    }

                    destinationSerial++;
                }
            }
        }

        private static void CollectBuildingCandidates(Transform buildingsRoot, List<Transform> destination)
        {
            if (buildingsRoot == null || destination == null)
            {
                return;
            }

            destination.Clear();
            for (int i = 0; i < buildingsRoot.childCount; i++)
            {
                Transform direct = buildingsRoot.GetChild(i);
                if (direct == null)
                {
                    continue;
                }

                bool collectedFromChildren = false;
                if (direct.childCount > 0 && direct.GetComponent<Renderer>() == null)
                {
                    for (int j = 0; j < direct.childCount; j++)
                    {
                        Transform nested = direct.GetChild(j);
                        if (nested == null)
                        {
                            continue;
                        }

                        if (nested.GetComponentInChildren<Renderer>(true) == null)
                        {
                            continue;
                        }

                        destination.Add(nested);
                        collectedFromChildren = true;
                    }
                }

                if (collectedFromChildren)
                {
                    continue;
                }

                if (direct.GetComponentInChildren<Renderer>(true) != null)
                {
                    destination.Add(direct);
                }
            }
        }

        private static string ResolveRegionIdForPosition(Vector3 worldPosition)
        {
            return RegionWorldLayout.ResolveRegionId(worldPosition);
        }
    }
}
