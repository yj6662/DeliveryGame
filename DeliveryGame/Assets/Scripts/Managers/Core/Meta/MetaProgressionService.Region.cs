using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed partial class MetaProgressionService
    {
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
