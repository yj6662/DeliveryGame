namespace DeliveryRun.Managers.Core
{
    public struct FuelStateChanged
    {
        public float Fuel01;
        public float CurrentFuel;
        public float MaxFuel;
    }

    public struct FuelDepleted
    {
    }

    public struct FuelRefuelStateChanged
    {
        public bool IsRefueling;
        public float CostPerSecond;
        public float FuelPerSecond;
    }
}
