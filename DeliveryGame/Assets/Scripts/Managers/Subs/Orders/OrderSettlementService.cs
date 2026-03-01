using System;
using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderSettlementService
    {
        internal void ProcessSuccess(
            ServiceRegistry services,
            EventBus events,
            GameClock clock,
            EconomyService economy,
            FoodStateConfigSO foodConfig,
            ModifierStackService stack,
            string offerId,
            int baseReward,
            double acceptedAt,
            double pickedUpAt)
        {
            float qualityMul = 1f;
            float quality = 1f;
            float temperature = 1f;
            float spill = 0f;

            FoodStateService food;
            if (services != null &&
                services.TryGet(out food) &&
                food != null)
            {
                if (food.TryGetState(offerId, out temperature, out spill, out quality))
                {
                    if (foodConfig != null)
                    {
                        qualityMul = food.ComputeRewardMultiplier(quality, foodConfig);
                    }

                    food.StopForOffer(offerId);
                }
            }

            float musicMul = stack != null ? stack.GetMul(RunStatId.RewardMultiplier) : 1f;
            float elapsedFromAccept = acceptedAt >= 0d ? (float)Math.Max(0d, clock.Now - acceptedAt) : 0f;
            float elapsedFromPickup = pickedUpAt >= 0d ? (float)Math.Max(0d, clock.Now - pickedUpAt) : 0f;

            int finalReward = OrderRewardCalculator.ComputeFinalReward(baseReward, musicMul, qualityMul, elapsedFromAccept);

            events.Publish(new FoodQualityComputed
            {
                OfferId = offerId,
                Temperature01 = temperature,
                Spill01 = spill,
                Quality01 = quality,
                RewardMultiplier = qualityMul,
                RatingDelta = 0f
            });

            if (economy != null)
            {
                economy.AddReward(finalReward, "order_complete");
            }

            events.Publish(new SessionBalanceChanged
            {
                Balance = economy != null ? economy.SessionBalance : 0,
                Delta = finalReward,
                Reason = "order_complete"
            });

            events.Publish(new OrderDeliveryReached
            {
                OfferId = offerId,
                Reward = finalReward
            });

            events.Publish(new OrderCompleted
            {
                OfferId = offerId,
                Reward = finalReward
            });

            events.Publish(new OrderObjectiveUpdated
            {
                OfferId = offerId,
                Text = offerId + " COMPLETE +$" + finalReward + " (" + elapsedFromAccept.ToString("0.0") + "s / " +
                       elapsedFromPickup.ToString("0.0") + "s)",
                DistanceMeters = 0f
            });
        }

        internal void ProcessTimeout(
            ServiceRegistry services,
            EventBus events,
            string offerId,
            string foodName,
            float deliveryLimitSeconds)
        {
            FoodStateService food;
            if (services != null &&
                services.TryGet(out food) &&
                food != null)
            {
                food.StopForOffer(offerId);
            }

            events.Publish(new OrderTimedOut
            {
                OfferId = offerId,
                FoodName = foodName,
                LimitSeconds = UnityEngine.Mathf.Max(1f, deliveryLimitSeconds)
            });

            events.Publish(new OrderObjectiveUpdated
            {
                OfferId = offerId,
                Text = offerId + " FAILED (TIME OUT)",
                DistanceMeters = 0f
            });
        }
    }
}
