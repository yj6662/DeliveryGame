using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class FuelService
    {
        public float MaxFuel { get; private set; }
        public float CurrentFuel { get; private set; }

        public bool IsEmpty => CurrentFuel <= 0.0001f;
        public float Fuel01 => MaxFuel > 0.0001f ? Mathf.Clamp01(CurrentFuel / MaxFuel) : 0f;

        public void Reset(float maxFuel)
        {
            MaxFuel = Mathf.Max(1f, maxFuel);
            CurrentFuel = MaxFuel;
        }

        public void RefillToFull()
        {
            CurrentFuel = MaxFuel;
        }

        public void AddFuel(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CurrentFuel = Mathf.Min(MaxFuel, CurrentFuel + amount);
        }

        public void Consume(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CurrentFuel = Mathf.Max(0f, CurrentFuel - amount);
        }
    }
}
