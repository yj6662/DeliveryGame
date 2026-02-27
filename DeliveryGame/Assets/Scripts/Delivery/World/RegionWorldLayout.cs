using UnityEngine;

namespace DeliveryRun.Delivery.World
{
    public readonly struct RegionLayoutDefinition
    {
        public readonly string RegionId;
        public readonly Vector2 Center;
        public readonly int BlocksX;
        public readonly int BlocksZ;

        public RegionLayoutDefinition(string regionId, Vector2 center, int blocksX, int blocksZ)
        {
            RegionId = regionId;
            Center = center;
            BlocksX = blocksX;
            BlocksZ = blocksZ;
        }
    }

    public static class RegionWorldLayout
    {
        // One expanded world map: central + 5 themed regions.
        private static readonly RegionLayoutDefinition[] Definitions =
        {
            // Regions are snapped so their boundary roads touch each other without overlap.
            new RegionLayoutDefinition("central", new Vector2(0f, 0f), 4, 4),
            // Distances are computed from half-span sums to avoid z-fighting floor overlap.
            new RegionLayoutDefinition("rushdistrict", new Vector2(301f, 0f), 5, 4),
            new RegionLayoutDefinition("frostlands", new Vector2(0f, 301f), 4, 5),
            new RegionLayoutDefinition("hillcrest", new Vector2(-301f, 0f), 5, 5),
            new RegionLayoutDefinition("stormcoast", new Vector2(0f, -270f), 4, 4),
            new RegionLayoutDefinition("oldtown", new Vector2(301f, 301f), 5, 5),
            new RegionLayoutDefinition("seaside", new Vector2(301f, -270f), 5, 4)
        };

        public static int Count => Definitions.Length;

        public static bool TryGetByIndex(int index, out RegionLayoutDefinition definition)
        {
            if (index >= 0 && index < Definitions.Length)
            {
                definition = Definitions[index];
                return true;
            }

            definition = default;
            return false;
        }

        public static bool TryGetById(string regionId, out RegionLayoutDefinition definition)
        {
            if (!string.IsNullOrEmpty(regionId))
            {
                for (int i = 0; i < Definitions.Length; i++)
                {
                    if (Definitions[i].RegionId == regionId)
                    {
                        definition = Definitions[i];
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }

        public static string ResolveRegionId(Vector3 worldPosition)
        {
            float bestScore = float.MaxValue;
            string bestRegion = "central";

            for (int i = 0; i < Definitions.Length; i++)
            {
                RegionLayoutDefinition def = Definitions[i];
                float dx = worldPosition.x - def.Center.x;
                float dz = worldPosition.z - def.Center.y;
                float score = (dx * dx) + (dz * dz);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRegion = def.RegionId;
                }
            }

            return bestRegion;
        }
    }
}
