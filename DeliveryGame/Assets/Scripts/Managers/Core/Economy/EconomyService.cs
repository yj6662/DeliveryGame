namespace DeliveryRun.Managers.Core
{
    public sealed class EconomyService
    {
        public int SessionBalance { get; private set; }

        public void ResetSession()
        {
            SessionBalance = 0;
        }

        public void AddReward(int amount, string reason)
        {
            if (amount <= 0)
            {
                return;
            }

            SessionBalance += amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (SessionBalance < amount)
            {
                return false;
            }

            SessionBalance -= amount;
            return true;
        }
    }
}
