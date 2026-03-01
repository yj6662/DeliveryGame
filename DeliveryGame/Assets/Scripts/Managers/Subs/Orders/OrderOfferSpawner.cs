using System.Collections.Generic;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderOfferSpawner
    {
        private const int BaseReward = 250;
        private const float BaseOfferTtlSeconds = 5f;
        private const float BaseRespawnDelaySeconds = 4.5f;
        private const float OfferTickInterval = 0.1f;
        private const int MaxActiveOrders = 3;
        private const float DefaultDeliveryLimitSeconds = 120f;

        private readonly OrderFoodSelectionService _foodSelectionService;
        private readonly OrderAnchorSelectionService _anchorSelectionService;
        private readonly OrderPendingOfferRuntime _pendingOfferRuntime;

        private int _pendingRespawnHandle = -1;
        private int _nextOrderId = 1;
        private int _nextOfferSerial = 1;
        private bool _missingAnchorLogged;

        public OrderOfferSpawner(
            OrderFoodSelectionService foodSelectionService,
            OrderAnchorSelectionService anchorSelectionService,
            OrderPendingOfferRuntime pendingOfferRuntime)
        {
            _foodSelectionService = foodSelectionService;
            _anchorSelectionService = anchorSelectionService;
            _pendingOfferRuntime = pendingOfferRuntime;
        }

        public bool HasPendingOffer => _pendingOfferRuntime.HasPendingOffer;
        public OrderPendingOffer CurrentOffer => _pendingOfferRuntime.Current;

        public void Reset()
        {
            _nextOrderId = 1;
            _nextOfferSerial = 1;
            _missingAnchorLogged = false;
            _pendingOfferRuntime.Clear();
        }

        public void SpawnOffer(
            bool runActive,
            List<OrderRuntimeActiveOrder> activeOrders,
            OrderAnchorCatalog catalog,
            string runRegionId,
            ModifierStackService stack,
            GameClock clock,
            EventBus events)
        {
            if (!runActive || _pendingOfferRuntime.HasPendingOffer || activeOrders.Count >= MaxActiveOrders)
            {
                return;
            }

            if (!catalog.HasAnchors)
            {
                if (!_missingAnchorLogged)
                {
                    _missingAnchorLogged = true;
                    Debug.LogWarning("[OrderFlowManager] No usable order anchors found in RunScene.");
                }

                return;
            }

            OrderFoodSelectionService.FoodSelection food =
                _foodSelectionService.PickFoodDefinition(runRegionId, DefaultDeliveryLimitSeconds);
            OrderBuildingAnchor restaurant = _anchorSelectionService.ResolveRestaurantForFood(
                food.FoodId,
                runRegionId,
                catalog.RestaurantAnchors,
                catalog.FoodRestaurantMap,
                OrderAnchorCatalog.ResolveAnchorRegion);

            OrderBuildingAnchor destination = _anchorSelectionService.ResolveRandomDestination(
                restaurant,
                runRegionId,
                catalog.DestinationAnchors,
                catalog.OrderAnchors,
                OrderAnchorCatalog.ResolveAnchorRegion);

            if (restaurant == null || destination == null)
            {
                if (!_missingAnchorLogged)
                {
                    _missingAnchorLogged = true;
                    Debug.LogWarning(
                        "[OrderFlowManager] Missing valid order anchors in selected region '" + runRegionId +
                        "'. Offer spawn deferred.");
                }

                return;
            }

            _missingAnchorLogged = false;
            string offerId = "A" + _nextOfferSerial;
            int orderId = _nextOrderId;
            _nextOfferSerial++;
            _nextOrderId++;

            string restaurantName = OrderAnchorCatalog.GetAnchorDisplayName(restaurant, "Restaurant");
            string destinationName = OrderAnchorCatalog.GetAnchorDisplayName(destination, "Destination");
            string pickupLabel = restaurantName + " \u2022 " + food.FoodName;
            float deliveryLimitSeconds = OrderRewardCalculator.ResolveDeliveryLimitSeconds(
                food.DeliveryLimitSeconds,
                food.IsSeafood,
                runRegionId);

            float ttl = OrderRewardCalculator.ResolveOfferTtlSeconds(BaseOfferTtlSeconds, stack);
            OrderPendingOffer pendingOffer = new OrderPendingOffer
            {
                Active = true,
                OfferId = offerId,
                OrderId = orderId,
                PickupName = pickupLabel,
                DeliveryName = destinationName,
                Reward = BaseReward,
                EndTime = clock.Now + ttl,
                RestaurantAnchor = restaurant,
                DestinationAnchor = destination,
                FoodId = food.FoodId,
                FoodName = food.FoodName,
                FoodTempDecayMultiplier = food.TempDecayMultiplier,
                FoodSpillGainMultiplier = food.SpillGainMultiplier,
                DeliveryLimitSeconds = deliveryLimitSeconds,
                FoodIsSeafood = food.IsSeafood,
                RegionId = runRegionId
            };
            _pendingOfferRuntime.Set(pendingOffer);

            events.Publish(new OfferSpawned
            {
                OfferId = pendingOffer.OfferId,
                TtlSeconds = ttl,
                PickupName = pendingOffer.PickupName,
                DeliveryName = pendingOffer.DeliveryName,
                Reward = pendingOffer.Reward,
                FoodId = pendingOffer.FoodId,
                FoodName = pendingOffer.FoodName
            });
        }

        public enum TickResult
        {
            None,
            Ticked,
            Expired
        }

        public TickResult TickPendingOffer(float unscaledDeltaTime, GameClock clock, out string offerId, out float remainingSeconds)
        {
            offerId = null;
            remainingSeconds = 0f;

            if (!_pendingOfferRuntime.HasPendingOffer)
            {
                return TickResult.None;
            }

            PendingOfferTickOutcome outcome =
                _pendingOfferRuntime.Tick(unscaledDeltaTime, OfferTickInterval, clock.Now, out remainingSeconds);

            if (outcome == PendingOfferTickOutcome.Expired)
            {
                offerId = _pendingOfferRuntime.Current.OfferId;
                return TickResult.Expired;
            }

            if (outcome == PendingOfferTickOutcome.Ticked)
            {
                offerId = _pendingOfferRuntime.Current.OfferId;
                return TickResult.Ticked;
            }

            return TickResult.None;
        }

        public string ClearPendingOffer()
        {
            if (!_pendingOfferRuntime.HasPendingOffer)
            {
                return null;
            }

            string offerId = _pendingOfferRuntime.Current.OfferId;
            _pendingOfferRuntime.Clear();
            return offerId;
        }

        public void ScheduleRespawnIfNeeded(
            bool runActive,
            bool isRunScene,
            List<OrderRuntimeActiveOrder> activeOrders,
            ModifierStackService stack,
            GameClock clock,
            System.Action onRespawnDue)
        {
            if (!runActive || !isRunScene || _pendingRespawnHandle >= 0 || _pendingOfferRuntime.HasPendingOffer ||
                activeOrders.Count >= MaxActiveOrders)
            {
                return;
            }

            _pendingRespawnHandle = clock.Schedule(
                OrderRewardCalculator.ResolveRespawnDelaySeconds(BaseRespawnDelaySeconds, stack),
                () =>
                {
                    _pendingRespawnHandle = -1;
                    onRespawnDue();
                });
        }

        public void CancelRespawn(GameClock clock)
        {
            if (_pendingRespawnHandle < 0)
            {
                return;
            }

            clock.Cancel(_pendingRespawnHandle);
            _pendingRespawnHandle = -1;
        }

        public void OnMusicChoiceModalStateChanged(bool isOpen, double now)
        {
            _pendingOfferRuntime.OnMusicChoiceModalStateChanged(isOpen, now);
        }
    }
}
