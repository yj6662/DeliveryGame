namespace DeliveryRun.Managers.Core
{
    public readonly struct RegionUnlockRule
    {
        public readonly string RegionId;
        public readonly string PreviousRegionId;
        public readonly int RequiredRunCash;
        public readonly float RequiredRunRating;
        public readonly int UnlockCost;

        public RegionUnlockRule(
            string regionId,
            string previousRegionId,
            int requiredRunCash,
            float requiredRunRating,
            int unlockCost)
        {
            RegionId = regionId;
            PreviousRegionId = previousRegionId;
            RequiredRunCash = requiredRunCash;
            RequiredRunRating = requiredRunRating;
            UnlockCost = unlockCost;
        }
    }

    public static class RegionProgressionCatalog
    {
        private static readonly RegionUnlockRule[] Rules =
        {
            new RegionUnlockRule("rushdistrict", "central", 800, 1.5f, 900),
            new RegionUnlockRule("frostlands", "rushdistrict", 1400, 2.0f, 1500),
            new RegionUnlockRule("hillcrest", "frostlands", 2000, 2.4f, 2200),
            new RegionUnlockRule("stormcoast", "hillcrest", 2800, 2.8f, 3200),
            new RegionUnlockRule("oldtown", "stormcoast", 3600, 3.1f, 4500),
            new RegionUnlockRule("seaside", "oldtown", 4200, 3.3f, 5200)
        };

        public static int RuleCount => Rules.Length;

        public static RegionUnlockRule GetRuleByIndex(int index)
        {
            if (index < 0 || index >= Rules.Length)
            {
                return default;
            }

            return Rules[index];
        }

        public static bool TryGetRule(string regionId, out RegionUnlockRule rule)
        {
            if (!string.IsNullOrEmpty(regionId))
            {
                for (int i = 0; i < Rules.Length; i++)
                {
                    if (Rules[i].RegionId == regionId)
                    {
                        rule = Rules[i];
                        return true;
                    }
                }
            }

            rule = default;
            return false;
        }

        public static bool TryGetNextLockedRegion(MetaProgressionService meta, out string regionId)
        {
            regionId = null;
            if (meta == null)
            {
                return false;
            }

            for (int i = 0; i < Rules.Length; i++)
            {
                if (!meta.IsRegionUnlocked(Rules[i].RegionId))
                {
                    regionId = Rules[i].RegionId;
                    return true;
                }
            }

            return false;
        }
    }
}
