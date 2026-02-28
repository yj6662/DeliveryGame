using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal static class OrderRewardCalculator
    {
        private const float DefaultDeliveryLimitSeconds = 120f;
        private const float SeasideDeliveryLimitClampMin = 45f;
        private const float GlobalDeliveryLimitClampMin = 60f;
        private const float DeliveryLimitClampMax = 240f;

        internal static int ComputeFinalReward(int baseReward, float rewardMultiplier, float qualityMultiplier, float elapsedFromAcceptSeconds)
        {
            float timeMultiplier = EvaluateDeliveryTimeMultiplier(elapsedFromAcceptSeconds);
            float raw = baseReward * rewardMultiplier * qualityMultiplier * timeMultiplier;
            return Mathf.Max(0, Mathf.RoundToInt(raw));
        }

        internal static float ResolveDeliveryLimitSeconds(float foodDeliveryLimitSeconds, bool isSeafood, string runRegionId)
        {
            float limit = foodDeliveryLimitSeconds > 0.1f ? foodDeliveryLimitSeconds : DefaultDeliveryLimitSeconds;
            bool seasideRegion = string.Equals(runRegionId, "seaside", System.StringComparison.Ordinal);

            if (seasideRegion)
            {
                limit *= isSeafood ? 0.70f : 0.82f;
                return Mathf.Clamp(limit, SeasideDeliveryLimitClampMin, DeliveryLimitClampMax);
            }

            if (isSeafood)
            {
                limit *= 0.88f;
            }

            return Mathf.Clamp(limit, GlobalDeliveryLimitClampMin, DeliveryLimitClampMax);
        }

        internal static float ResolveOfferTtlSeconds(float baseOfferTtlSeconds, ModifierStackService stack)
        {
            float ttl = baseOfferTtlSeconds;
            if (stack != null)
            {
                ttl *= stack.GetMul(RunStatId.OfferAcceptTtlMultiplier);
            }

            return Mathf.Clamp(ttl, 1.5f, 30f);
        }

        internal static float ResolveRespawnDelaySeconds(float baseRespawnDelaySeconds, ModifierStackService stack)
        {
            float delay = baseRespawnDelaySeconds;
            if (stack != null)
            {
                delay *= stack.GetMul(RunStatId.OfferRespawnDelayMultiplier);
            }

            return Mathf.Clamp(delay, 0.25f, 15f);
        }

        private static float EvaluateDeliveryTimeMultiplier(float elapsedSeconds)
        {
            if (elapsedSeconds <= 30f) return 1.30f;
            if (elapsedSeconds <= 50f) return 1.15f;
            if (elapsedSeconds <= 75f) return 1.00f;
            if (elapsedSeconds <= 105f) return 0.90f;
            return 0.80f;
        }
    }
}
