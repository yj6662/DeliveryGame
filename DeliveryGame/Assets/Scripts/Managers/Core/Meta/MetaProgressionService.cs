using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class MetaProgressionService
    {
        private const string TotalCashKey = "meta.total_cash";
        private const string RegionKeyPrefix = "meta.region.";
        private const string UpgradeKeyPrefix = "meta.upgrade.";

        private static readonly string[] RegionIds = { "central", "harbor", "uptown", "industrial" };
        private static readonly string[] UpgradeIds = { "bike_speed", "bike_grip", "bike_brake", "reward_bonus" };

        private readonly bool[] _regionUnlocked = new bool[4];
        private readonly int[] _upgradeLevels = new int[4];

        public int TotalCash { get; private set; }

        public void Load()
        {
            TotalCash = Mathf.Max(0, PlayerPrefs.GetInt(TotalCashKey, 0));

            for (int i = 0; i < RegionIds.Length; i++)
            {
                int defaultValue = i == 0 ? 1 : 0;
                _regionUnlocked[i] = PlayerPrefs.GetInt(RegionKeyPrefix + RegionIds[i], defaultValue) != 0;
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                _upgradeLevels[i] = Mathf.Max(0, PlayerPrefs.GetInt(UpgradeKeyPrefix + UpgradeIds[i], 0));
            }
        }

        public void Save()
        {
            PlayerPrefs.SetInt(TotalCashKey, Mathf.Max(0, TotalCash));

            for (int i = 0; i < RegionIds.Length; i++)
            {
                PlayerPrefs.SetInt(RegionKeyPrefix + RegionIds[i], _regionUnlocked[i] ? 1 : 0);
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                PlayerPrefs.SetInt(UpgradeKeyPrefix + UpgradeIds[i], Mathf.Max(0, _upgradeLevels[i]));
            }

            PlayerPrefs.Save();
        }

        public void AddTotalCash(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            TotalCash += amount;
            Save();
        }

        public bool TrySpendTotalCash(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (TotalCash < amount)
            {
                return false;
            }

            TotalCash -= amount;
            Save();
            return true;
        }

        public bool IsRegionUnlocked(string regionId)
        {
            int idx = IndexOfRegion(regionId);
            return idx >= 0 && _regionUnlocked[idx];
        }

        public bool UnlockRegion(string regionId)
        {
            int idx = IndexOfRegion(regionId);
            if (idx < 0 || _regionUnlocked[idx])
            {
                return false;
            }

            _regionUnlocked[idx] = true;
            Save();
            return true;
        }

        public int GetUpgradeLevel(string upgradeId)
        {
            int idx = IndexOfUpgrade(upgradeId);
            return idx >= 0 ? _upgradeLevels[idx] : 0;
        }

        public bool SetUpgradeLevel(string upgradeId, int level)
        {
            int idx = IndexOfUpgrade(upgradeId);
            if (idx < 0)
            {
                return false;
            }

            int safeLevel = Mathf.Max(0, level);
            if (_upgradeLevels[idx] == safeLevel)
            {
                return false;
            }

            _upgradeLevels[idx] = safeLevel;
            Save();
            return true;
        }

        public int CopyRegionIdsNonAlloc(string[] destination)
        {
            if (destination == null || destination.Length == 0)
            {
                return 0;
            }

            int count = destination.Length < RegionIds.Length ? destination.Length : RegionIds.Length;
            for (int i = 0; i < count; i++)
            {
                destination[i] = RegionIds[i];
            }

            return count;
        }

        private static int IndexOfRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return -1;
            }

            for (int i = 0; i < RegionIds.Length; i++)
            {
                if (RegionIds[i] == regionId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfUpgrade(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId))
            {
                return -1;
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                if (UpgradeIds[i] == upgradeId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
