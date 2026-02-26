using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class RatingConfigSO : ScriptableObject
    {
        public float StartRating = 5f;

        [Header("Quality thresholds (high to low)")]
        public float Q_Excellent = 0.85f;
        public float Q_Good = 0.65f;
        public float Q_Ok = 0.45f;
        public float Q_Bad = 0.25f;

        [Header("Delta")]
        public float Delta_Excellent = 0.20f;
        public float Delta_Good = 0.10f;
        public float Delta_Ok = 0.00f;
        public float Delta_Bad = -0.20f;
        public float Delta_Terrible = -0.40f;
    }
}
