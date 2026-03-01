namespace DeliveryRun.Managers.Subs
{
    internal struct SectorThemeDefinition
    {
        internal string RegionId;
        internal string DisplayName;
        internal string Description;
        internal float OfferTtlMul;
        internal float OfferRespawnMul;
        internal float TempDecayMul;
        internal float SpillGainMul;
        internal float SpeedMul;
        internal float TurnMul;
        internal float AccelMul;
        internal float RewardMul;
    }

    internal static class SectorThemeDefinitions
    {
        private static readonly SectorThemeDefinition[] Definitions =
        {
            new SectorThemeDefinition
            {
                RegionId = "central",
                DisplayName = "Central",
                Description = "Balanced city center.",
                OfferTtlMul = 1f,
                OfferRespawnMul = 1f,
                TempDecayMul = 1f,
                SpillGainMul = 1f,
                SpeedMul = 1f,
                TurnMul = 1f,
                AccelMul = 1f,
                RewardMul = 1f
            },
            new SectorThemeDefinition
            {
                RegionId = "rushdistrict",
                DisplayName = "Rush District",
                Description = "Orders rotate faster with tighter acceptance windows.",
                OfferTtlMul = 0.72f,
                OfferRespawnMul = 0.82f,
                TempDecayMul = 1.05f,
                SpillGainMul = 1.08f,
                SpeedMul = 1.05f,
                TurnMul = 1f,
                AccelMul = 1.04f,
                RewardMul = 1.02f
            },
            new SectorThemeDefinition
            {
                RegionId = "frostlands",
                DisplayName = "Frostlands",
                Description = "Cold zone. Food temperature drops much faster.",
                OfferTtlMul = 1f,
                OfferRespawnMul = 1f,
                TempDecayMul = 1.45f,
                SpillGainMul = 1.04f,
                SpeedMul = 0.96f,
                TurnMul = 1.02f,
                AccelMul = 0.94f,
                RewardMul = 1.08f
            },
            new SectorThemeDefinition
            {
                RegionId = "hillcrest",
                DisplayName = "Hillcrest",
                Description = "Hills and ramps. More spill risk and lower acceleration.",
                OfferTtlMul = 0.94f,
                OfferRespawnMul = 1f,
                TempDecayMul = 1.06f,
                SpillGainMul = 1.35f,
                SpeedMul = 0.95f,
                TurnMul = 0.92f,
                AccelMul = 0.88f,
                RewardMul = 1.12f
            },
            new SectorThemeDefinition
            {
                RegionId = "stormcoast",
                DisplayName = "Storm Coast",
                Description = "Wet roads. Spill increases quickly.",
                OfferTtlMul = 0.9f,
                OfferRespawnMul = 0.95f,
                TempDecayMul = 1.2f,
                SpillGainMul = 1.55f,
                SpeedMul = 0.97f,
                TurnMul = 0.95f,
                AccelMul = 0.93f,
                RewardMul = 1.15f
            },
            new SectorThemeDefinition
            {
                RegionId = "oldtown",
                DisplayName = "Old Town",
                Description = "Dense blocks and premium tips for clean deliveries.",
                OfferTtlMul = 1.08f,
                OfferRespawnMul = 1.1f,
                TempDecayMul = 1f,
                SpillGainMul = 1.1f,
                SpeedMul = 0.93f,
                TurnMul = 1.1f,
                AccelMul = 0.92f,
                RewardMul = 1.2f
            },
            new SectorThemeDefinition
            {
                RegionId = "seaside",
                DisplayName = "Seaside",
                Description = "Sea breeze and wet lanes. Faster chill, cleaner lines earn more.",
                OfferTtlMul = 0.98f,
                OfferRespawnMul = 0.92f,
                TempDecayMul = 1.28f,
                SpillGainMul = 1.22f,
                SpeedMul = 1.01f,
                TurnMul = 0.98f,
                AccelMul = 0.97f,
                RewardMul = 1.24f
            }
        };

        internal static SectorThemeDefinition Find(string regionId)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].RegionId == regionId)
                {
                    return Definitions[i];
                }
            }

            return Definitions[0];
        }
    }
}
