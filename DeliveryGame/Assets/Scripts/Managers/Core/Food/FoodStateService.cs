using System.Collections.Generic;
using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class FoodStateService
    {
        private sealed class OfferFoodState
        {
            internal float Temperature01;
            internal float Spill01;
            internal float OrderTempDecayMultiplier = 1f;
            internal float OrderSpillGainMultiplier = 1f;
        }

        private readonly Dictionary<string, OfferFoodState> _statesByOffer = new Dictionary<string, OfferFoodState>(8);

        public bool IsActive => _statesByOffer.Count > 0;
        public int ActiveCount => _statesByOffer.Count;
        public string ActiveOfferId
        {
            get
            {
                foreach (KeyValuePair<string, OfferFoodState> pair in _statesByOffer)
                {
                    return pair.Key;
                }

                return string.Empty;
            }
        }

        public float Temperature01
        {
            get
            {
                OfferFoodState state;
                return TryGetPrimaryState(out state) ? state.Temperature01 : 0f;
            }
        }

        public float Spill01
        {
            get
            {
                OfferFoodState state;
                return TryGetPrimaryState(out state) ? state.Spill01 : 0f;
            }
        }

        public void Reset()
        {
            _statesByOffer.Clear();
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
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            OfferFoodState state;
            if (!_statesByOffer.TryGetValue(offerId, out state))
            {
                state = new OfferFoodState();
                _statesByOffer.Add(offerId, state);
            }

            state.OrderTempDecayMultiplier = Mathf.Max(0.05f, orderTempDecayMultiplier);
            state.OrderSpillGainMultiplier = Mathf.Max(0.05f, orderSpillGainMultiplier);
            if (cfg == null)
            {
                state.Temperature01 = 1f;
                state.Spill01 = 0f;
                return;
            }

            state.Temperature01 = Mathf.Clamp01(cfg.StartTemperature01);
            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            state.Spill01 = Mathf.Clamp(cfg.StartSpill01, 0f, spillMax);
        }

        public void StopForOffer(string offerId)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            _statesByOffer.Remove(offerId);
        }

        public void TickUnscaledAll(
            float dt,
            float speed,
            float yawRateAbs,
            float decel,
            float collisionImpulse,
            FoodStateConfigSO cfg,
            float temperatureDecayMultiplier,
            float spillGainMultiplier)
        {
            if (_statesByOffer.Count <= 0 || cfg == null || dt <= 0f)
            {
                return;
            }

            float globalTempMul = Mathf.Max(0f, temperatureDecayMultiplier);
            float globalSpillMul = Mathf.Max(0f, spillGainMultiplier);
            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            foreach (KeyValuePair<string, OfferFoodState> pair in _statesByOffer)
            {
                OfferFoodState state = pair.Value;
                float tempMul = globalTempMul * state.OrderTempDecayMultiplier;
                float spillMul = globalSpillMul * state.OrderSpillGainMultiplier;

                float tempDecay = cfg.TempDecayPerSecond * tempMul;
                if (speed > cfg.HighSpeedThreshold)
                {
                    tempDecay += cfg.HighSpeedExtraDecayPerSecond * tempMul;
                }

                state.Temperature01 = Mathf.Clamp01(state.Temperature01 - (tempDecay * dt));

                float spillInc =
                    (yawRateAbs * speed * cfg.SpillFromTurn * dt * spillMul) +
                    (decel * cfg.SpillFromBrake * dt * spillMul) +
                    (collisionImpulse * cfg.SpillFromCollision * spillMul);

                state.Spill01 = Mathf.Clamp(state.Spill01 + spillInc, 0f, spillMax);
            }
        }

        public float ComputeQuality01()
        {
            OfferFoodState state;
            if (!TryGetPrimaryState(out state))
            {
                return 1f;
            }

            return ComputeQuality01(state.Temperature01, state.Spill01);
        }

        public float ComputeQuality01(float temperature01, float spill01)
        {
            return Mathf.Clamp01(Mathf.Clamp01(temperature01) * (1f - Mathf.Clamp01(spill01)));
        }

        public bool TryGetState(string offerId, out float temperature01, out float spill01, out float quality01)
        {
            temperature01 = 0f;
            spill01 = 0f;
            quality01 = 0f;
            if (string.IsNullOrEmpty(offerId))
            {
                return false;
            }

            OfferFoodState state;
            if (!_statesByOffer.TryGetValue(offerId, out state))
            {
                return false;
            }

            temperature01 = state.Temperature01;
            spill01 = state.Spill01;
            quality01 = ComputeQuality01(state.Temperature01, state.Spill01);
            return true;
        }

        public int CopyActiveOfferIdsNonAlloc(List<string> destination)
        {
            if (destination == null)
            {
                return 0;
            }

            destination.Clear();
            foreach (KeyValuePair<string, OfferFoodState> pair in _statesByOffer)
            {
                destination.Add(pair.Key);
            }

            return destination.Count;
        }

        public void AddSpill(string offerId, float amount, FoodStateConfigSO cfg)
        {
            if (string.IsNullOrEmpty(offerId) || cfg == null || amount <= 0f)
            {
                return;
            }

            OfferFoodState state;
            if (!_statesByOffer.TryGetValue(offerId, out state))
            {
                return;
            }

            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            state.Spill01 = Mathf.Clamp(state.Spill01 + amount, 0f, spillMax);
        }

        public void AddSpillToAll(float amount, FoodStateConfigSO cfg)
        {
            if (_statesByOffer.Count <= 0 || cfg == null || amount <= 0f)
            {
                return;
            }

            float spillMax = Mathf.Max(0f, cfg.SpillClampMax);
            foreach (KeyValuePair<string, OfferFoodState> pair in _statesByOffer)
            {
                OfferFoodState state = pair.Value;
                state.Spill01 = Mathf.Clamp(state.Spill01 + amount, 0f, spillMax);
            }
        }

        public float ComputeRewardMultiplier(float quality01, FoodStateConfigSO cfg)
        {
            if (cfg == null)
            {
                return 1f;
            }

            return Mathf.Lerp(cfg.RewardMulAtQuality0, cfg.RewardMulAtQuality1, Mathf.Clamp01(quality01));
        }

        private bool TryGetPrimaryState(out OfferFoodState state)
        {
            foreach (KeyValuePair<string, OfferFoodState> pair in _statesByOffer)
            {
                state = pair.Value;
                return true;
            }

            state = null;
            return false;
        }
    }
}
