using System;
using DeliveryRun.Managers.Core;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class RunModifierRuntime
    {
        private static bool TryMapStatKey(string statKey, out RunStatId stat)
        {
            if (string.Equals(statKey, "move_speed_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.PlayerMoveSpeedMultiplier;
                return true;
            }

            if (string.Equals(statKey, "bike_grip_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.BikeLateralGripMultiplier;
                return true;
            }

            if (string.Equals(statKey, "bike_turn_mul", StringComparison.Ordinal) ||
                string.Equals(statKey, "turn_sensitivity_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.BikeTurnSensitivityMultiplier;
                return true;
            }

            if (string.Equals(statKey, "bike_accel_mul", StringComparison.Ordinal) ||
                string.Equals(statKey, "acceleration_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.BikeAccelerationMultiplier;
                return true;
            }

            if (string.Equals(statKey, "bike_brake_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.BikeBrakeForceMultiplier;
                return true;
            }

            if (string.Equals(statKey, "reward_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.RewardMultiplier;
                return true;
            }

            if (string.Equals(statKey, "music_high_tier_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.MusicHighTierChanceMultiplier;
                return true;
            }

            if (string.Equals(statKey, "food_temp_decay_mul", StringComparison.Ordinal) ||
                string.Equals(statKey, "temp_decay_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.FoodTemperatureDecayMultiplier;
                return true;
            }

            if (string.Equals(statKey, "spill_gain_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.FoodSpillGainMultiplier;
                return true;
            }

            if (string.Equals(statKey, "offer_accept_ttl_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.OfferAcceptTtlMultiplier;
                return true;
            }

            if (string.Equals(statKey, "offer_respawn_delay_mul", StringComparison.Ordinal) ||
                string.Equals(statKey, "offer_interval_mul", StringComparison.Ordinal))
            {
                stat = RunStatId.OfferRespawnDelayMultiplier;
                return true;
            }

            stat = default;
            return false;
        }
    }
}
