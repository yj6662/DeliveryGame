using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed partial class MetaProgressionService
    {
        public readonly struct SaveSlotSummary
        {
            public readonly int SlotIndex;
            public readonly bool HasData;
            public readonly int TotalCash;
            public readonly string SelectedRegionId;

            public SaveSlotSummary(int slotIndex, bool hasData, int totalCash, string selectedRegionId)
            {
                SlotIndex = slotIndex;
                HasData = hasData;
                TotalCash = totalCash;
                SelectedRegionId = selectedRegionId;
            }
        }

        private const string ActiveSaveSlotKey = "meta.active_slot";
        private const int SaveSlotCountConst = 3;
        private const int DefaultSaveSlotIndex = 0;

        private const string TotalCashKeySuffix = "total_cash";
        private const string RegionKeyPrefixSuffix = "region.";
        private const string RegionBestRunCashKeyPrefixSuffix = "region_best_run_cash.";
        private const string RegionBestRunRatingKeyPrefixSuffix = "region_best_run_rating.";
        private const string UpgradeKeyPrefixSuffix = "upgrade.";
        private const string SelectedRegionKeySuffix = "selected_region";
        private const string SchemaVersionKeySuffix = "schema_version";

        private const string LegacyTotalCashKey = "meta.total_cash";
        private const string LegacyRegionKeyPrefix = "meta.region.";
        private const string LegacyRegionBestRunCashKeyPrefix = "meta.region_best_run_cash.";
        private const string LegacyRegionBestRunRatingKeyPrefix = "meta.region_best_run_rating.";
        private const string LegacyUpgradeKeyPrefix = "meta.upgrade.";
        private const string LegacySelectedRegionKey = "meta.selected_region";
        private const string LegacySchemaVersionKey = "meta.schema_version";

        private const int CurrentSchemaVersion = 2;

        private static readonly string[] RegionIds = MetaProgressionConstants.RegionIds;
        private static readonly string[] UpgradeIds = MetaProgressionConstants.UpgradeIds;

        private readonly bool[] _regionUnlocked = new bool[RegionIds.Length];
        private readonly int[] _regionBestRunCash = new int[RegionIds.Length];
        private readonly float[] _regionBestRunRating = new float[RegionIds.Length];
        private readonly int[] _upgradeLevels = new int[UpgradeIds.Length];
        private string _selectedRegionId = RegionIds[0];
        private int _activeSaveSlotIndex = DefaultSaveSlotIndex;

        public int TotalCash { get; private set; }
        public string SelectedRegionId => _selectedRegionId;
        public int RegionCount => RegionIds.Length;
        public int UpgradeCount => UpgradeIds.Length;
        public int SaveSlotCount => SaveSlotCountConst;
        public int ActiveSaveSlotIndex => _activeSaveSlotIndex;

        public bool IsValidSaveSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SaveSlotCountConst;
        }

        public bool SlotHasData(int slotIndex)
        {
            if (!IsValidSaveSlot(slotIndex))
            {
                return false;
            }

            return PlayerPrefs.HasKey(BuildSlotKey(slotIndex, SchemaVersionKeySuffix));
        }

        public bool TryGetSlotSummary(int slotIndex, out SaveSlotSummary summary)
        {
            if (!IsValidSaveSlot(slotIndex))
            {
                summary = default;
                return false;
            }

            bool hasData = SlotHasData(slotIndex);
            if (!hasData)
            {
                summary = new SaveSlotSummary(slotIndex, false, 0, RegionIds[0]);
                return true;
            }

            int schemaVersion = PlayerPrefs.GetInt(BuildSlotKey(slotIndex, SchemaVersionKeySuffix), 0);
            if (schemaVersion != CurrentSchemaVersion)
            {
                summary = new SaveSlotSummary(slotIndex, false, 0, RegionIds[0]);
                return true;
            }

            int totalCash = Mathf.Max(0, PlayerPrefs.GetInt(BuildSlotKey(slotIndex, TotalCashKeySuffix), 0));
            string selectedRegion = PlayerPrefs.GetString(BuildSlotKey(slotIndex, SelectedRegionKeySuffix), RegionIds[0]);
            if (string.IsNullOrEmpty(selectedRegion))
            {
                selectedRegion = RegionIds[0];
            }

            summary = new SaveSlotSummary(slotIndex, true, totalCash, selectedRegion);
            return true;
        }

        public bool SaveToSlot(int slotIndex)
        {
            if (!IsValidSaveSlot(slotIndex))
            {
                return false;
            }

            _activeSaveSlotIndex = slotIndex;
            Save();
            return true;
        }

        public bool LoadSlot(int slotIndex)
        {
            if (!IsValidSaveSlot(slotIndex))
            {
                return false;
            }

            Load(slotIndex);
            return true;
        }

        public void Load()
        {
            int slotIndex = PlayerPrefs.GetInt(ActiveSaveSlotKey, DefaultSaveSlotIndex);
            Load(slotIndex);
        }

        public void Load(int slotIndex)
        {
            if (!IsValidSaveSlot(slotIndex))
            {
                slotIndex = DefaultSaveSlotIndex;
            }

            _activeSaveSlotIndex = slotIndex;
            PlayerPrefs.SetInt(ActiveSaveSlotKey, _activeSaveSlotIndex);

            int storedSchemaVersion = PlayerPrefs.GetInt(BuildSlotKey(_activeSaveSlotIndex, SchemaVersionKeySuffix), 0);
            if (storedSchemaVersion != CurrentSchemaVersion)
            {
                if (_activeSaveSlotIndex == DefaultSaveSlotIndex && TryMigrateLegacySave())
                {
                    PlayerPrefs.Save();
                    return;
                }

                ResetAllProgress(false);
                Save();
                return;
            }

            TotalCash = Mathf.Max(0, PlayerPrefs.GetInt(BuildSlotKey(_activeSaveSlotIndex, TotalCashKeySuffix), 0));

            for (int i = 0; i < RegionIds.Length; i++)
            {
                int defaultValue = i == 0 ? 1 : 0;
                _regionUnlocked[i] = PlayerPrefs.GetInt(BuildSlotKey(_activeSaveSlotIndex, RegionKeyPrefixSuffix + RegionIds[i]), defaultValue) != 0;
                _regionBestRunCash[i] = Mathf.Max(0, PlayerPrefs.GetInt(BuildSlotKey(_activeSaveSlotIndex, RegionBestRunCashKeyPrefixSuffix + RegionIds[i]), 0));
                _regionBestRunRating[i] = Mathf.Clamp(PlayerPrefs.GetFloat(BuildSlotKey(_activeSaveSlotIndex, RegionBestRunRatingKeyPrefixSuffix + RegionIds[i]), 0f), 0f, 5f);
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                _upgradeLevels[i] = Mathf.Max(0, PlayerPrefs.GetInt(BuildSlotKey(_activeSaveSlotIndex, UpgradeKeyPrefixSuffix + UpgradeIds[i]), 0));
            }

            string selected = PlayerPrefs.GetString(BuildSlotKey(_activeSaveSlotIndex, SelectedRegionKeySuffix), RegionIds[0]);
            if (!TrySetSelectedRegionInternal(selected))
            {
                _selectedRegionId = GetFirstUnlockedRegion();
            }

            PlayerPrefs.Save();
        }

        public void Save()
        {
            PlayerPrefs.SetInt(ActiveSaveSlotKey, _activeSaveSlotIndex);
            PlayerPrefs.SetInt(BuildSlotKey(_activeSaveSlotIndex, SchemaVersionKeySuffix), CurrentSchemaVersion);
            PlayerPrefs.SetInt(BuildSlotKey(_activeSaveSlotIndex, TotalCashKeySuffix), Mathf.Max(0, TotalCash));

            for (int i = 0; i < RegionIds.Length; i++)
            {
                PlayerPrefs.SetInt(BuildSlotKey(_activeSaveSlotIndex, RegionKeyPrefixSuffix + RegionIds[i]), _regionUnlocked[i] ? 1 : 0);
                PlayerPrefs.SetInt(BuildSlotKey(_activeSaveSlotIndex, RegionBestRunCashKeyPrefixSuffix + RegionIds[i]), Mathf.Max(0, _regionBestRunCash[i]));
                PlayerPrefs.SetFloat(BuildSlotKey(_activeSaveSlotIndex, RegionBestRunRatingKeyPrefixSuffix + RegionIds[i]), Mathf.Clamp(_regionBestRunRating[i], 0f, 5f));
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                PlayerPrefs.SetInt(BuildSlotKey(_activeSaveSlotIndex, UpgradeKeyPrefixSuffix + UpgradeIds[i]), Mathf.Max(0, _upgradeLevels[i]));
            }

            PlayerPrefs.SetString(BuildSlotKey(_activeSaveSlotIndex, SelectedRegionKeySuffix), _selectedRegionId);
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

        private bool TryMigrateLegacySave()
        {
            int legacySchemaVersion = PlayerPrefs.GetInt(LegacySchemaVersionKey, 0);
            if (legacySchemaVersion != CurrentSchemaVersion)
            {
                return false;
            }

            TotalCash = Mathf.Max(0, PlayerPrefs.GetInt(LegacyTotalCashKey, 0));

            for (int i = 0; i < RegionIds.Length; i++)
            {
                int defaultValue = i == 0 ? 1 : 0;
                _regionUnlocked[i] = PlayerPrefs.GetInt(LegacyRegionKeyPrefix + RegionIds[i], defaultValue) != 0;
                _regionBestRunCash[i] = Mathf.Max(0, PlayerPrefs.GetInt(LegacyRegionBestRunCashKeyPrefix + RegionIds[i], 0));
                _regionBestRunRating[i] = Mathf.Clamp(PlayerPrefs.GetFloat(LegacyRegionBestRunRatingKeyPrefix + RegionIds[i], 0f), 0f, 5f);
            }

            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                _upgradeLevels[i] = Mathf.Max(0, PlayerPrefs.GetInt(LegacyUpgradeKeyPrefix + UpgradeIds[i], 0));
            }

            string selected = PlayerPrefs.GetString(LegacySelectedRegionKey, RegionIds[0]);
            if (!TrySetSelectedRegionInternal(selected))
            {
                _selectedRegionId = GetFirstUnlockedRegion();
            }

            Save();
            return true;
        }

        private static string BuildSlotKey(int slotIndex, string suffix)
        {
            return "meta.slot." + slotIndex + "." + suffix;
        }
    }
}
