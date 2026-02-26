using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class FoodStateService
    {
        public bool IsActive { get; private set; }
        public string ActiveOfferId { get; private set; }
        public float Temperature01 { get; private set; }
        public float Spill01 { get; private set; }

        public void Reset()
        {
            IsActive = false;
            ActiveOfferId = string.Empty;
            Temperature01 = 0f;
            Spill01 = 0f;
        }

        public void StartForOffer(string offerId, FoodStateConfigSO cfg)
        {
            IsActive = true;
            ActiveOfferId = offerId ?? string.Empty;
            if (cfg == null)
            {
                Temperature01 = 1f;
                Spill01 = 0f;
                return;
            }

            Temperature01 = Mathf.Clamp01(cfg.StartTemperature01);
            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            Spill01 = Mathf.Clamp(cfg.StartSpill01, 0f, spillMax);
        }

        public void TickUnscaled(
            float dt,
            float speed,
            float yawRateAbs,
            float decel,
            float collisionImpulse,
            FoodStateConfigSO cfg)
        {
            if (!IsActive || cfg == null || dt <= 0f)
            {
                return;
            }

            float tempDecay = cfg.TempDecayPerSecond;
            if (speed > cfg.HighSpeedThreshold)
            {
                tempDecay += cfg.HighSpeedExtraDecayPerSecond;
            }

            Temperature01 = Mathf.Clamp01(Temperature01 - (tempDecay * dt));

            float spillInc =
                (yawRateAbs * speed * cfg.SpillFromTurn * dt) +
                (decel * cfg.SpillFromBrake * dt) +
                (collisionImpulse * cfg.SpillFromCollision);

            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            Spill01 = Mathf.Clamp(Spill01 + spillInc, 0f, spillMax);
        }

        public float ComputeQuality01()
        {
            return Mathf.Clamp01(Temperature01 * (1f - Spill01));
        }

        public float ComputeRewardMultiplier(float quality01, FoodStateConfigSO cfg)
        {
            if (cfg == null)
            {
                return 1f;
            }

            return Mathf.Lerp(cfg.RewardMulAtQuality0, cfg.RewardMulAtQuality1, Mathf.Clamp01(quality01));
        }
    }
}
