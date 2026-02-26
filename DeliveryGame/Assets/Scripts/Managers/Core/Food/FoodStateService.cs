using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class FoodStateService
    {
        private float _orderTempDecayMultiplier = 1f;
        private float _orderSpillGainMultiplier = 1f;

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
            _orderTempDecayMultiplier = 1f;
            _orderSpillGainMultiplier = 1f;
        }

        public void StartForOffer(string offerId, FoodStateConfigSO cfg)
        {
            StartForOffer(offerId, cfg, 1f, 1f);
        }

        public void StartForOffer(
            string offerId,
            FoodStateConfigSO cfg,
            float orderTempDecayMultiplier,
            float orderSpillGainMultiplier)
        {
            IsActive = true;
            ActiveOfferId = offerId ?? string.Empty;
            _orderTempDecayMultiplier = Mathf.Max(0.05f, orderTempDecayMultiplier);
            _orderSpillGainMultiplier = Mathf.Max(0.05f, orderSpillGainMultiplier);
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
            FoodStateConfigSO cfg,
            float temperatureDecayMultiplier,
            float spillGainMultiplier)
        {
            if (!IsActive || cfg == null || dt <= 0f)
            {
                return;
            }

            float tempMul = Mathf.Max(0f, temperatureDecayMultiplier) * _orderTempDecayMultiplier;
            float spillMul = Mathf.Max(0f, spillGainMultiplier) * _orderSpillGainMultiplier;

            float tempDecay = cfg.TempDecayPerSecond * tempMul;
            if (speed > cfg.HighSpeedThreshold)
            {
                tempDecay += cfg.HighSpeedExtraDecayPerSecond * tempMul;
            }

            Temperature01 = Mathf.Clamp01(Temperature01 - (tempDecay * dt));

            float spillInc =
                (yawRateAbs * speed * cfg.SpillFromTurn * dt * spillMul) +
                (decel * cfg.SpillFromBrake * dt * spillMul) +
                (collisionImpulse * cfg.SpillFromCollision * spillMul);

            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            Spill01 = Mathf.Clamp(Spill01 + spillInc, 0f, spillMax);
        }

        public float ComputeQuality01()
        {
            return Mathf.Clamp01(Temperature01 * (1f - Spill01));
        }

        public void AddSpill(float amount, FoodStateConfigSO cfg)
        {
            if (!IsActive || cfg == null || amount <= 0f)
            {
                return;
            }

            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            Spill01 = Mathf.Clamp(Spill01 + amount, 0f, spillMax);
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
