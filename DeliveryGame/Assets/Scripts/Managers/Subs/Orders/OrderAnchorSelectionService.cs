using System;
using System.Collections.Generic;
using DeliveryRun.Delivery.Orders;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderAnchorSelectionService
    {
        internal OrderBuildingAnchor ResolveRestaurantForFood(
            string foodId,
            string preferredRegionId,
            List<OrderBuildingAnchor> restaurantAnchors,
            Dictionary<string, OrderBuildingAnchor> foodRestaurantMap,
            Func<OrderBuildingAnchor, string> resolveAnchorRegion)
        {
            if (restaurantAnchors == null || restaurantAnchors.Count <= 0 || string.IsNullOrEmpty(preferredRegionId) || resolveAnchorRegion == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(foodId) &&
                foodRestaurantMap != null &&
                foodRestaurantMap.TryGetValue(foodId, out OrderBuildingAnchor mapped) &&
                mapped != null &&
                string.Equals(resolveAnchorRegion(mapped), preferredRegionId, StringComparison.Ordinal))
            {
                return mapped;
            }

            return SelectRandomAnchorInRegion(restaurantAnchors, null, preferredRegionId, resolveAnchorRegion);
        }

        internal OrderBuildingAnchor ResolveRandomDestination(
            OrderBuildingAnchor restaurant,
            string preferredRegionId,
            List<OrderBuildingAnchor> destinationAnchors,
            List<OrderBuildingAnchor> orderAnchors,
            Func<OrderBuildingAnchor, string> resolveAnchorRegion)
        {
            if (string.IsNullOrEmpty(preferredRegionId) || resolveAnchorRegion == null)
            {
                return null;
            }

            OrderBuildingAnchor regional = SelectRandomAnchorInRegion(destinationAnchors, restaurant, preferredRegionId, resolveAnchorRegion);
            if (regional != null)
            {
                return regional;
            }

            return SelectRandomAnchorInRegion(orderAnchors, restaurant, preferredRegionId, resolveAnchorRegion);
        }

        private static OrderBuildingAnchor SelectRandomAnchorInRegion(
            List<OrderBuildingAnchor> anchors,
            OrderBuildingAnchor excluded,
            string regionId,
            Func<OrderBuildingAnchor, string> resolveAnchorRegion)
        {
            if (anchors == null || anchors.Count <= 0 || string.IsNullOrEmpty(regionId) || resolveAnchorRegion == null)
            {
                return null;
            }

            OrderBuildingAnchor selected = null;
            int selectable = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                OrderBuildingAnchor candidate = anchors[i];
                if (candidate == null || candidate == excluded)
                {
                    continue;
                }

                if (!string.Equals(resolveAnchorRegion(candidate), regionId, StringComparison.Ordinal))
                {
                    continue;
                }

                selectable++;
                if (UnityEngine.Random.Range(0, selectable) == 0)
                {
                    selected = candidate;
                }
            }

            return selected;
        }
    }
}
