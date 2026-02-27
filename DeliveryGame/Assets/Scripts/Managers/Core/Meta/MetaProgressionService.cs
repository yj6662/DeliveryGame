using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed class MetaProgressionService
    {
        private const string TotalCashKey = "meta.total_cash";
        private const string RegionKeyPrefix = "meta.region.";
        private const string RegionBestRunCashKeyPrefix = "meta.region_best_run_cash.";
        private const string RegionBestRunRatingKeyPrefix = "meta.region_best_run_rating.";
        private const string UpgradeKeyPrefix = "meta.upgrade.";
        private const string SelectedRegionKey = "meta.selected_region";
        private const string SchemaVersionKey = "meta.schema_version";
        private const int CurrentSchemaVersion = 2;

        private static readonly string[] RegionIds =
        {
            "central",
            "rushdistrict",
            "frostlands",
            "hillcrest",
            "stormcoast",
            "oldtown",
            "seaside"
        };

        private static readonly string[] UpgradeIds =
        {
            "bike_speed",
            "bike_turn",
            "bike_accel",
            "music_luck",
            "bike_grip",
            "bike_brake",
            "reward_bonus"
        };

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

        public bool IsRegionUnlocked(string regionId)
        {
            int idx = IndexOfRegion(regionId);
            return idx >= 0 && _regionUnlocked[idx];
        }

        public int GetBestRunCash(string regionId)
        {
            int idx = IndexOfRegion(regionId);
            if (idx < 0)
            {
                return 0;
            }

            return _regionBestRunCash[idx];
        }

        public float GetBestRunRating(string regionId)
        {
            int idx = IndexOfRegion(regionId);
            if (idx < 0)
            {
                return 0f;
            }

            return _regionBestRunRating[idx];
        }

        public bool RegisterRegionRunResult(string regionId, int runCash, float endRating)
        {
            int idx = IndexOfRegion(regionId);
            if (idx < 0)
            {
                return false;
            }

            int safeCash = Mathf.Max(0, runCash);
            float safeRating = Mathf.Clamp(endRating, 0f, 5f);
            bool changed = false;

            if (safeCash > _regionBestRunCash[idx])
            {
                _regionBestRunCash[idx] = safeCash;
                changed = true;
            }

            if (safeRating > _regionBestRunRating[idx])
            {
                _regionBestRunRating[idx] = safeRating;
                changed = true;
            }

            if (changed)
            {
                Save();
            }

            return changed;
        }

        public bool UnlockRegion(string regionId)
        {
            int idx = IndexOfRegion(regionId);
            if (idx < 0 || _regionUnlocked[idx])
            {
                return false;
            }

            _regionUnlocked[idx] = true;
            int selectedIdx = IndexOfRegion(_selectedRegionId);
            if (selectedIdx < 0 || !_regionUnlocked[selectedIdx])
            {
                _selectedRegionId = regionId;
            }
            Save();
            return true;
        }

        public bool TrySetSelectedRegion(string regionId)
        {
            if (!TrySetSelectedRegionInternal(regionId))
            {
                return false;
            }

            Save();
            return true;
        }

        public bool SelectNextUnlockedRegion()
        {
            int current = IndexOfRegion(_selectedRegionId);
            if (current < 0)
            {
                _selectedRegionId = GetFirstUnlockedRegion();
                Save();
                return true;
            }

            for (int offset = 1; offset <= RegionIds.Length; offset++)
            {
                int idx = current + offset;
                if (idx >= RegionIds.Length)
                {
                    idx -= RegionIds.Length;
                }

                if (!_regionUnlocked[idx])
                {
                    continue;
                }

                if (RegionIds[idx] == _selectedRegionId)
                {
                    return false;
                }

                _selectedRegionId = RegionIds[idx];
                Save();
                return true;
            }

            return false;
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

        public int CopyUpgradeIdsNonAlloc(string[] destination)
        {
            if (destination == null || destination.Length == 0)
            {
                return 0;
            }

            int count = destination.Length < UpgradeIds.Length ? destination.Length : UpgradeIds.Length;
            for (int i = 0; i < count; i++)
            {
                destination[i] = UpgradeIds[i];
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

        private bool TrySetSelectedRegionInternal(string regionId)
        {
            int idx = IndexOfRegion(regionId);
            if (idx < 0 || !_regionUnlocked[idx])
            {
                return false;
            }

            _selectedRegionId = RegionIds[idx];
            return true;
        }

        private string GetFirstUnlockedRegion()
        {
            for (int i = 0; i < RegionIds.Length; i++)
            {
                if (_regionUnlocked[i])
                {
                    return RegionIds[i];
                }
            }

            return RegionIds[0];
        }
    }
}
