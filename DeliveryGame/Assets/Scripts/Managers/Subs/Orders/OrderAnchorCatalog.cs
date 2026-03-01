using System.Collections.Generic;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderAnchorCatalog
    {
        private readonly List<OrderBuildingAnchor> _restaurantAnchors = new List<OrderBuildingAnchor>(32);
        private readonly List<OrderBuildingAnchor> _destinationAnchors = new List<OrderBuildingAnchor>(96);
        private readonly List<OrderBuildingAnchor> _orderAnchors = new List<OrderBuildingAnchor>(128);
        private readonly Dictionary<string, OrderBuildingAnchor> _foodRestaurantMap =
            new Dictionary<string, OrderBuildingAnchor>(16);

        private readonly OrderBuildingRoleAssigner _buildingRoleAssigner = new OrderBuildingRoleAssigner();
        private readonly OrderFoodSelectionService _foodSelectionService;

        private bool _ready;

        public OrderAnchorCatalog(OrderFoodSelectionService foodSelectionService)
        {
            _foodSelectionService = foodSelectionService;
        }

        public List<OrderBuildingAnchor> RestaurantAnchors => _restaurantAnchors;
        public List<OrderBuildingAnchor> DestinationAnchors => _destinationAnchors;
        public List<OrderBuildingAnchor> OrderAnchors => _orderAnchors;
        public Dictionary<string, OrderBuildingAnchor> FoodRestaurantMap => _foodRestaurantMap;
        public bool IsReady => _ready;
        public bool HasAnchors => _restaurantAnchors.Count > 0 && _orderAnchors.Count > 0;

        public void EnsureReady(MetaProgressionService meta, ServiceRegistry services)
        {
            if (_ready && _restaurantAnchors.Count > 0 && _orderAnchors.Count > 0)
            {
                return;
            }

            Rebuild(meta, services);
        }

        public void Rebuild(MetaProgressionService meta, ServiceRegistry services)
        {
            _restaurantAnchors.Clear();
            _destinationAnchors.Clear();
            _orderAnchors.Clear();
            _foodRestaurantMap.Clear();
            _ready = false;

            _buildingRoleAssigner.EnsureAssigned();

            OrderBuildingAnchor[] anchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsSortMode.None);
            for (int i = 0; i < anchors.Length; i++)
            {
                OrderBuildingAnchor anchor = anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                string regionId = ResolveAnchorRegion(anchor);
                if (!IsRegionUnlocked(regionId, meta, services))
                {
                    continue;
                }

                if (anchor.Role == OrderBuildingRole.GasStation)
                {
                    continue;
                }

                _orderAnchors.Add(anchor);
                if (anchor.Role == OrderBuildingRole.Restaurant)
                {
                    _restaurantAnchors.Add(anchor);
                }
                else
                {
                    _destinationAnchors.Add(anchor);
                }
            }

            if (_restaurantAnchors.Count <= 0 && _orderAnchors.Count > 0)
            {
                _restaurantAnchors.Add(_orderAnchors[0]);
            }

            if (_destinationAnchors.Count <= 0)
            {
                for (int i = 0; i < _orderAnchors.Count; i++)
                {
                    OrderBuildingAnchor anchor = _orderAnchors[i];
                    if (anchor == null)
                    {
                        continue;
                    }

                    if (anchor.Role == OrderBuildingRole.Restaurant)
                    {
                        continue;
                    }

                    _destinationAnchors.Add(anchor);
                }
            }

            if (_destinationAnchors.Count <= 0 && _orderAnchors.Count > 0)
            {
                for (int i = 0; i < _orderAnchors.Count; i++)
                {
                    _destinationAnchors.Add(_orderAnchors[i]);
                }
            }

            _foodSelectionService.BuildFoodRestaurantMap(_restaurantAnchors, _foodRestaurantMap);
            _ready = _restaurantAnchors.Count > 0 && _orderAnchors.Count > 0;
        }

        public void Clear()
        {
            _restaurantAnchors.Clear();
            _destinationAnchors.Clear();
            _orderAnchors.Clear();
            _foodRestaurantMap.Clear();
            _ready = false;
        }

        public static string ResolveAnchorRegion(OrderBuildingAnchor anchor)
        {
            if (anchor == null)
            {
                return "central";
            }

            string regionId = RegionWorldLayout.ResolveRegionId(anchor.transform.position);
            if (anchor.RegionId != regionId)
            {
                anchor.RegionId = regionId;
            }

            return regionId;
        }

        public static string GetAnchorDisplayName(OrderBuildingAnchor anchor, string fallback)
        {
            if (anchor == null)
            {
                return fallback;
            }

            if (!string.IsNullOrEmpty(anchor.DisplayName))
            {
                return anchor.DisplayName;
            }

            return anchor.name;
        }

        private static bool IsRegionUnlocked(string regionId, MetaProgressionService meta, ServiceRegistry services)
        {
            if (meta == null)
            {
                services.TryGet(out meta);
            }

            if (meta == null)
            {
                return true;
            }

            return meta.IsRegionUnlocked(regionId);
        }
    }
}
