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
