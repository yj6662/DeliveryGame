namespace DeliveryRun.Managers.Core
{
    internal readonly struct LobbyRegionUnlockSnapshot
    {
        internal LobbyRegionUnlockSnapshot(
            string regionId,
            string previousRegionId,
            int requiredRunCash,
            float requiredRunRating,
            int unlockCost,
            int bestRunCash,
            float bestRunRating,
            bool previousRegionUnlocked,
            bool canAfford)
        {
            RegionId = regionId;
            PreviousRegionId = previousRegionId;
            RequiredRunCash = requiredRunCash;
            RequiredRunRating = requiredRunRating;
            UnlockCost = unlockCost;
            BestRunCash = bestRunCash;
            BestRunRating = bestRunRating;
            PreviousRegionUnlocked = previousRegionUnlocked;
            CanAfford = canAfford;
            MeetsPerformance = previousRegionUnlocked &&
                               bestRunCash >= requiredRunCash &&
                               bestRunRating >= requiredRunRating;
            CanUnlock = MeetsPerformance && canAfford;
        }

        internal string RegionId { get; }
        internal string PreviousRegionId { get; }
        internal int RequiredRunCash { get; }
        internal float RequiredRunRating { get; }
        internal int UnlockCost { get; }
        internal int BestRunCash { get; }
        internal float BestRunRating { get; }
        internal bool PreviousRegionUnlocked { get; }
        internal bool CanAfford { get; }
        internal bool MeetsPerformance { get; }
        internal bool CanUnlock { get; }
    }

    internal static class LobbyRegionMetaUtil
    {
        internal static string FormatRegionName(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return "CENTRAL";
            }

            string lower = regionId.ToLowerInvariant();
            if (lower == "rushdistrict") return "RUSH DISTRICT";
            if (lower == "frostlands") return "FROSTLANDS";
            if (lower == "hillcrest") return "HILLCREST";
            if (lower == "stormcoast") return "STORM COAST";
            if (lower == "oldtown") return "OLD TOWN";
            if (lower == "seaside") return "SEASIDE";
            if (lower == "central") return "CENTRAL";

            return lower.ToUpperInvariant();
        }

        internal static bool TryBuildNextUnlockSnapshot(
            MetaProgressionService meta,
            out LobbyRegionUnlockSnapshot snapshot)
        {
            snapshot = default;
            if (meta == null)
            {
                return false;
            }

            string regionId;
            RegionUnlockRule rule;
            if (!RegionProgressionCatalog.TryGetNextLockedRegion(meta, out regionId) ||
                !RegionProgressionCatalog.TryGetRule(regionId, out rule))
            {
                return false;
            }

            int bestRunCash = meta.GetBestRunCash(rule.PreviousRegionId);
            float bestRunRating = meta.GetBestRunRating(rule.PreviousRegionId);
            bool previousUnlocked = meta.IsRegionUnlocked(rule.PreviousRegionId);
            bool canAfford = meta.TotalCash >= rule.UnlockCost;

            snapshot = new LobbyRegionUnlockSnapshot(
                rule.RegionId,
                rule.PreviousRegionId,
                rule.RequiredRunCash,
                rule.RequiredRunRating,
                rule.UnlockCost,
                bestRunCash,
                bestRunRating,
                previousUnlocked,
                canAfford);

            return true;
        }
    }
}
