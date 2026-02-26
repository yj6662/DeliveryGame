using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class FoodStateConfigSO : ScriptableObject
    {
        [Header("Temperature")]
        public float StartTemperature01 = 1f;
        public float TempDecayPerSecond = 0.0022f;
        public float HighSpeedExtraDecayPerSecond = 0.0010f;
        public float HighSpeedThreshold = 18f;

        [Header("Spill")]
        public float StartSpill01 = 0f;
        public float SpillFromTurn = 0.0012f;
        public float SpillFromBrake = 0.0010f;
        public float SpillFromCollision = 0.00008f;
        public float SpillClampMax = 1f;

        [Header("Quality -> Reward")]
        public float RewardMulAtQuality0 = 0.60f;
        public float RewardMulAtQuality1 = 1.20f;
    }
}
