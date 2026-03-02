namespace DeliveryRun.Managers.Core
{
    public static class MetaProgressionConstants
    {
        // Matches MetaProgressionRuntime permanent-upgrade application.
        public static readonly float[] UpgradePerLevelMulDelta =
        {
            0.06f, // bike_speed
            0.08f, // bike_turn
            0.07f, // bike_accel
            0.15f, // music_luck
            0.05f, // bike_grip
            0.05f, // bike_brake
            0.04f  // reward_bonus
        };

        public static readonly string[] RegionIds =
        {
            "central",
            "rushdistrict",
            "frostlands",
            "hillcrest",
            "stormcoast",
            "oldtown",
            "seaside"
        };

        public static readonly string[] UpgradeIds =
        {
            "bike_speed",
            "bike_turn",
            "bike_accel",
            "music_luck",
            "bike_grip",
            "bike_brake",
            "reward_bonus"
        };

        public static readonly string[] UpgradeLabels =
        {
            "Speed",
            "Turn",
            "Accel",
            "Luck",
            "Grip",
            "Brake",
            "Reward"
        };
    }
}
