using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed partial class MetaProgressionService
    {
        private const string TotalCashKey = "meta.total_cash";
        private const string RegionKeyPrefix = "meta.region.";
        private const string RegionBestRunCashKeyPrefix = "meta.region_best_run_cash.";
        private const string RegionBestRunRatingKeyPrefix = "meta.region_best_run_rating.";
        private const string UpgradeKeyPrefix = "meta.upgrade.";
        private const string SelectedRegionKey = "meta.selected_region";
        private const string SchemaVersionKey = "meta.schema_version";
        private const int CurrentSchemaVersion = 2;

        private static readonly string[] RegionIds = MetaProgressionConstants.RegionIds;
        private static readonly string[] UpgradeIds = MetaProgressionConstants.UpgradeIds;

        private readonly bool[] _regionUnlocked = new bool[RegionIds.Length];
        private readonly int[] _regionBestRunCash = new int[RegionIds.Length];
        private readonly float[] _regionBestRunRating = new float[RegionIds.Length];
        private readonly int[] _upgradeLevels = new int[UpgradeIds.Length];
        private string _selectedRegionId = RegionIds[0];

        public int TotalCash { get; private set; }
        public string SelectedRegionId => _selectedRegionId;
        public int RegionCount => RegionIds.Length;
        public int UpgradeCount => UpgradeIds.Length;

        public void Load()
        {
            int storedSchemaVersion = PlayerPrefs.GetInt(SchemaVersionKey, 0);
            if (storedSchemaVersion != CurrentSchemaVersion)
            {
                ResetAllProgress(false);
                Save();
                return;
            }

            TotalCash = Mathf.Max(0, PlayerPrefs.GetInt(TotalCashKey, 0));

            for (int i = 0; i < RegionIds.Length; i++)
            {
                int defaultValue = i == 0 ? 1 : 0;
                _regionUnlocked[i] = PlayerPrefs.GetInt(RegionKeyPrefix + RegionIds[i], defaultValue) != 0;
                _regionBestRunCash[i] = Mathf.Max(0, PlayerPrefs.GetInt(RegionBestRunCashKeyPrefix + RegionIds[i], 0));
                _regionBestRunRating[i] = Mathf.Clamp(PlayerPrefs.GetFloat(RegionBestRunRatingKeyPrefix + RegionIds[i], 0f), 0f, 5f);
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                _upgradeLevels[i] = Mathf.Max(0, PlayerPrefs.GetInt(UpgradeKeyPrefix + UpgradeIds[i], 0));
            }

            string selected = PlayerPrefs.GetString(SelectedRegionKey, RegionIds[0]);
            if (!TrySetSelectedRegionInternal(selected))
            {
                _selectedRegionId = GetFirstUnlockedRegion();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetInt(SchemaVersionKey, CurrentSchemaVersion);
            PlayerPrefs.SetInt(TotalCashKey, Mathf.Max(0, TotalCash));

            for (int i = 0; i < RegionIds.Length; i++)
            {
                PlayerPrefs.SetInt(RegionKeyPrefix + RegionIds[i], _regionUnlocked[i] ? 1 : 0);
                PlayerPrefs.SetInt(RegionBestRunCashKeyPrefix + RegionIds[i], Mathf.Max(0, _regionBestRunCash[i]));
                PlayerPrefs.SetFloat(RegionBestRunRatingKeyPrefix + RegionIds[i], Mathf.Clamp(_regionBestRunRating[i], 0f, 5f));
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                PlayerPrefs.SetInt(UpgradeKeyPrefix + UpgradeIds[i], Mathf.Max(0, _upgradeLevels[i]));
            }

            PlayerPrefs.SetString(SelectedRegionKey, _selectedRegionId);
            PlayerPrefs.Save();
        }

        public void ResetAllProgress(bool save = true)
        {
            TotalCash = 0;

            for (int i = 0; i < RegionIds.Length; i++)
            {
                _regionUnlocked[i] = i == 0;
                _regionBestRunCash[i] = 0;
                _regionBestRunRating[i] = 0f;
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                _upgradeLevels[i] = 0;
            }

            _selectedRegionId = RegionIds[0];

            if (save)
            {
                Save();
            }
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

    }
}
