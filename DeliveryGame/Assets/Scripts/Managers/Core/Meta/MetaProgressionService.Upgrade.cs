using UnityEngine;

namespace DeliveryRun.Managers.Core
{
    public sealed partial class MetaProgressionService
    {
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
