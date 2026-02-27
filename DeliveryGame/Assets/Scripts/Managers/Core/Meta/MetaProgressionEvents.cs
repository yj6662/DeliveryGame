namespace DeliveryRun.Managers.Core
{
    public struct MetaBalanceChanged
    {
        public int TotalCash;
        public int Delta;
    }

    public struct RegionUnlocked
    {
        public string RegionId;
        public int RequiredTotalCash;
    }

    public struct SelectedRegionChanged
    {
        public string RegionId;
    }

    public struct SelectNextRegionRequested
    {
    }

    public struct UnlockRegionRequested
    {
        public string RegionId;
    }

    public struct RegionUnlockStatusChanged
    {
        public string RegionId;
        public string PreviousRegionId;
        public int UnlockCost;
        public int RequiredRunCash;
        public float RequiredRunRating;
        public int BestRunCashInPrevious;
        public float BestRunRatingInPrevious;
        public bool PreviousRegionUnlocked;
        public bool MeetsPerformance;
        public bool CanAfford;
        public bool CanUnlockNow;
    }

    public struct RegionUnlockFailed
    {
        public string RegionId;
        public string Reason;
    }

    public struct PermanentUpgradePurchaseRequested
    {
        public string UpgradeId;
    }

    public struct PermanentUpgradeChanged
    {
        public string UpgradeId;
        public int Level;
    }
}
