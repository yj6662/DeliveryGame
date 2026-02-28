using System;
using System.Collections.Generic;
using System.IO;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
        public static class DeliveryRunMegaWorldGenerator
        {
            private const string RunScenePath = "Assets/Scenes/Run/RunScene.unity";
            private const string ExternalsRoot = "Assets/Externals";
            private const string MaterialRoot = "Assets/Materials/Generated";
            private const string RegionMaterialRoot = MaterialRoot + "/Regions";
            private const string ConnectorRoadMatPath = MaterialRoot + "/RunCity_Road_Connector.mat";
            private const string BarrierMatPath = MaterialRoot + "/RunCity_RegionBarrier.mat";
        private const string RoadsKitRoot = "Assets/Externals/kenney_city-kit-roads";
        private const string RoadsKitModelRoot = RoadsKitRoot + "/Models/FBX format";
        private const string RoadSkinStraightPath = RoadsKitModelRoot + "/road-straight.fbx";
        private const string RoadSkinSquarePath = RoadsKitModelRoot + "/road-square.fbx";
        private const string RoadSkinCornerPath = RoadsKitModelRoot + "/road-bend-sidewalk.fbx";
        private const string RoadSkinCornerFallbackPath = RoadsKitModelRoot + "/road-curve-intersection.fbx";
        private const string RoadSkinCornerSecondFallbackPath = RoadsKitModelRoot + "/road-bend.fbx";
        private const string RoadSkinTJunctionPath = RoadsKitModelRoot + "/road-intersection.fbx";
        private const string RoadSkinTJunctionFallbackPath = RoadsKitModelRoot + "/road-intersection-path.fbx";
        private const string RoadSkinCrossroadPath = RoadsKitModelRoot + "/road-crossroad-path.fbx";
        private const string RoadSkinEndPath = RoadsKitModelRoot + "/road-end.fbx";
        private const string SignalSkinCrossPath = RoadsKitModelRoot + "/light-square-cross.fbx";
        private const string SignalSkinDoublePath = RoadsKitModelRoot + "/light-square-double.fbx";
        private const string SignalSkinSinglePath = RoadsKitModelRoot + "/light-square.fbx";
        private const string StreetLightSkinPrimaryPath = RoadsKitModelRoot + "/light-curved.fbx";
        private const string StreetLightSkinCrossPath = RoadsKitModelRoot + "/light-curved-cross.fbx";
        private const string StreetLightSkinDoublePath = RoadsKitModelRoot + "/light-curved-double.fbx";
        private const float RoadSkinYOffset = 0.0125f;
        private const float JunctionLaneJoinOverlap = 0.28f;
        private const float JunctionVisualScaleNudge = 1.0125f;
        private const float StraightVisualScaleNudge = 1.0075f;
        private const float CrossroadYawOffset = 90f;
        private const float TJunctionLocalZScaleRatio = 0.95752f;
        private const float TJunctionForwardOffset = 0.2319f;
        private const float CornerVisualScaleNudge = 1.02f;
        private const float CornerDiagonalOffset = 0f;
        private const float JunctionSignalOffset = 8.5f;
        private const float JunctionStreetLightOffset = 10.5f;

        private const float BlockSize = 40f;
        private const float BlockHeight = 0.55f;
        private const float BlockY = 0.275f;
        private const float BlockFoundationDepth = 7.5f;
        private const float BlockFoundationTopOffset = 0.10f;
        private const float RoadWidth = 22f;
        private const float RoadHeight = 0.18f;
        private const float LotInset = 10f;
        private const float BlockEdgeWallThickness = 1.2f;
        private const float BlockEdgeWallHeight = 1.6f;
        private const float BlockEdgeWallExtend = 0.8f;
        private const float GateThickness = 2.4f;
        private const float GateHeight = 4.2f;
        private const float GateBaseY = 2.1f;
        private const float GateSupportY = -0.22f;
        private const float GateSupportHeight = 0.45f;
        private const float BuildingMinHeightBase = 4.0f;
        private const float BuildingMaxHeightBase = 6.2f;
        private const float CurbBarrierTopMinY = 1.6f;
        private const float CurbBarrierBottomY = -4.0f;
        private const StaticEditorFlags GeneratedStaticFlags =
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic;
        private static GameObject s_roadSkinStraightPrefab;
        private static GameObject s_roadSkinSquarePrefab;
        private static GameObject s_roadSkinCornerPrefab;
        private static GameObject s_roadSkinTJunctionPrefab;
        private static GameObject s_roadSkinCrossroadPrefab;
        private static GameObject s_roadSkinEndPrefab;
        private static GameObject s_signalSkinSinglePrefab;
        private static GameObject s_signalSkinPrefab;
        private static GameObject s_signalSkinDoublePrefab;
        private static GameObject s_streetLightSkinPrefab;
        private static GameObject s_streetLightSkinCrossPrefab;
        private static GameObject s_streetLightSkinDoublePrefab;
        private static bool s_roadKitMetricsReady;
        private static float s_roadVisualUniformScale = 1f;
        private static float s_straightSourceLength = 1f;
        private static float s_straightSourceWidth = 1f;
        private static bool s_straightLongAxisX = true;
        private static float s_junctionWorldSizeX = RoadWidth;
        private static float s_junctionWorldSizeZ = RoadWidth;

        private readonly struct RegionVisualTheme
        {
            public readonly string RegionId;
            public readonly Color RoadColor;
            public readonly Color BlockColor;
            public readonly Color BuildingTint;
            public readonly float BuildingTintStrength;
            public readonly float RoadSmoothness;
            public readonly float BlockSmoothness;
            public readonly float BlockHeightMul;
            public readonly float LotInsetMul;
            public readonly float BuildingFootprintMul;
            public readonly float BuildingHeightMul;
            public readonly float BuildingJitterMeters;
            public readonly float RotationJitterDeg;
            public readonly bool PreferSkyline;
            public readonly float SeedOffset;

            public RegionVisualTheme(
                string regionId,
                Color roadColor,
                Color blockColor,
                Color buildingTint,
                float buildingTintStrength,
                float roadSmoothness,
                float blockSmoothness,
                float blockHeightMul,
                float lotInsetMul,
                float buildingFootprintMul,
                float buildingHeightMul,
                float buildingJitterMeters,
                float rotationJitterDeg,
                bool preferSkyline,
                float seedOffset)
            {
                RegionId = regionId;
                RoadColor = roadColor;
                BlockColor = blockColor;
                BuildingTint = buildingTint;
                BuildingTintStrength = buildingTintStrength;
                RoadSmoothness = roadSmoothness;
                BlockSmoothness = blockSmoothness;
                BlockHeightMul = blockHeightMul;
                LotInsetMul = lotInsetMul;
                BuildingFootprintMul = buildingFootprintMul;
                BuildingHeightMul = buildingHeightMul;
                BuildingJitterMeters = buildingJitterMeters;
                RotationJitterDeg = rotationJitterDeg;
                PreferSkyline = preferSkyline;
                SeedOffset = seedOffset;
            }
        }

        private struct RegionRuntimeInfo
        {
            public RegionLayoutDefinition Def;
            public float SpanX;
            public float SpanZ;
        }

        private readonly struct RegionConnectionDefinition
        {
            public readonly string FromRegionId;
            public readonly string ToRegionId;
            public readonly string ConnectorName;
            public readonly string GateRegionId;

            public RegionConnectionDefinition(string fromRegionId, string toRegionId, string connectorName, string gateRegionId)
            {
                FromRegionId = fromRegionId;
                ToRegionId = toRegionId;
                ConnectorName = connectorName;
                GateRegionId = gateRegionId;
            }
        }

        private sealed class BuildingEntry
        {
            public GameObject Go;
            public string RegionId;
            public int RegionOrder;
        }

        private enum CardinalDirection
        {
            North,
            East,
            South,
            West
        }

        private static readonly RegionConnectionDefinition[] RegionConnections =
        {
            new RegionConnectionDefinition("central", "rushdistrict", "Connector_Central_Rush", "rushdistrict"),
            new RegionConnectionDefinition("central", "frostlands", "Connector_Central_Frost", "frostlands"),
            new RegionConnectionDefinition("central", "hillcrest", "Connector_Central_Hill", "hillcrest"),
            new RegionConnectionDefinition("central", "stormcoast", "Connector_Central_Storm", "stormcoast"),
            new RegionConnectionDefinition("rushdistrict", "oldtown", "Connector_Rush_Oldtown", "oldtown"),
            new RegionConnectionDefinition("frostlands", "oldtown", "Connector_Frost_Oldtown", "oldtown"),
            new RegionConnectionDefinition("stormcoast", "seaside", "Connector_Storm_Seaside", "seaside"),
            new RegionConnectionDefinition("oldtown", "seaside", "Connector_Oldtown_Seaside", "seaside")
        };

        private static readonly RegionVisualTheme[] RegionThemes =
        {
            new RegionVisualTheme("central", new Color(0.16f, 0.17f, 0.18f, 1f), new Color(0.43f, 0.44f, 0.45f, 1f), new Color(0.98f, 0.99f, 1.00f, 1f), 0.22f, 0.66f, 0.35f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 12f, true, 1.1f),
            new RegionVisualTheme("rushdistrict", new Color(0.15f, 0.13f, 0.18f, 1f), new Color(0.34f, 0.30f, 0.40f, 1f), new Color(0.95f, 0.86f, 1.00f, 1f), 0.42f, 0.58f, 0.30f, 1.05f, 0.96f, 1.06f, 1.12f, 0.82f, 8f, true, 2.3f),
            new RegionVisualTheme("frostlands", new Color(0.14f, 0.18f, 0.23f, 1f), new Color(0.74f, 0.82f, 0.88f, 1f), new Color(0.84f, 0.93f, 1.00f, 1f), 0.36f, 0.62f, 0.26f, 0.95f, 1.05f, 0.95f, 1.02f, 1.15f, 15f, false, 3.7f),
            new RegionVisualTheme("hillcrest", new Color(0.20f, 0.18f, 0.15f, 1f), new Color(0.49f, 0.42f, 0.35f, 1f), new Color(1.00f, 0.92f, 0.82f, 1f), 0.40f, 0.64f, 0.32f, 1.12f, 1.08f, 0.96f, 1.08f, 1.35f, 18f, false, 4.9f),
            new RegionVisualTheme("stormcoast", new Color(0.10f, 0.12f, 0.13f, 1f), new Color(0.28f, 0.31f, 0.34f, 1f), new Color(0.82f, 0.89f, 0.95f, 1f), 0.34f, 0.78f, 0.37f, 0.92f, 1.02f, 0.94f, 1.00f, 1.28f, 14f, false, 5.5f),
            new RegionVisualTheme("oldtown", new Color(0.22f, 0.19f, 0.17f, 1f), new Color(0.50f, 0.44f, 0.38f, 1f), new Color(0.96f, 0.84f, 0.73f, 1f), 0.44f, 0.61f, 0.31f, 1.03f, 0.98f, 1.08f, 1.06f, 0.95f, 10f, false, 6.8f),
            new RegionVisualTheme("seaside", new Color(0.19f, 0.19f, 0.17f, 1f), new Color(0.82f, 0.76f, 0.62f, 1f), new Color(0.92f, 1.00f, 0.90f, 1f), 0.40f, 0.71f, 0.28f, 0.90f, 1.08f, 0.90f, 0.96f, 1.22f, 12f, false, 7.6f)
        };

        private static readonly string[] BuildingNameIncludeKeywords =
        {
            "building",
            "tower",
            "apartment",
            "house",
            "shop",
            "hotel",
            "office",
            "mall",
            "skyscraper"
        };

        private static readonly string[] BuildingNameExcludeKeywords =
        {
            "road",
            "street",
            "intersection",
            "corner",
            "bridge",
            "sidewalk",
            "tree",
            "fence",
            "sign",
            "light",
            "lamp",
            "traffic",
            "bus",
            "truck",
            "car",
            "van",
            "container",
            "ramp",
            "stair",
            "bench",
            "billboard",
            "prop",
            "spawn",
            "npc",
            "water",
            "river",
            "boat",
            "low_detail",
            "low detail"
        };

        private static readonly string[] CentralThemeKeywords =
        {
            "commercial",
            "city-kit-commercial",
            "building-kit"
        };

        private static readonly string[] RushDistrictThemeKeywords =
        {
            "retro",
            "retro-urban",
            "commercial"
        };

        private static readonly string[] FrostlandsThemeKeywords =
        {
            "suburban",
            "building-kit",
            "blocky"
        };

        private static readonly string[] HillcrestThemeKeywords =
        {
            "suburban",
            "building-kit",
            "residential"
        };

        private static readonly string[] StormcoastThemeKeywords =
        {
            "industrial",
            "city-kit-industrial",
            "warehouse"
        };

        private static readonly string[] OldTownThemeKeywords =
        {
            "retro",
            "building-kit",
            "commercial"
        };

        private static readonly string[] SeasideThemeKeywords =
        {
            "suburban",
            "commercial",
            "building-kit",
            "industrial"
        };

        [MenuItem("Tools/DeliveryRun/Generate MegaWorld (Unified Roads + Signals)")]
        public static void GenerateAll()
        {
            string scenePath = ResolveScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new InvalidOperationException("[MegaWorldGenerator] Run scene not found.");
            }

            CacheRoadSkinPrefabs();
            CacheSignalSkinPrefab();

            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialRoot);
            ResetRegionMaterialFolder();
            EnsureFolder(RegionMaterialRoot);
            Material connectorRoadMat = EnsureMaterial(
                ConnectorRoadMatPath,
                new Color(0.14f, 0.14f, 0.15f, 1f),
                0.10f,
                0.68f);
            Material barrierMat = EnsureTransparentMaterial(BarrierMatPath, new Color(0.44f, 0.80f, 1.00f, 0.30f));

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException("[MegaWorldGenerator] Failed to open scene: " + scenePath);
            }

            GameObject cityRoot = GetOrCreateRoot("CityRoot");
            int removedDuplicateCityRoots = RemoveDuplicateCityRoots(scene, cityRoot);
            int removedLegacyRoots = RemoveLegacyRoots(scene, cityRoot);
            int removedTransient = RemoveTransientSceneObjects(scene);
            Transform roadsRoot = GetOrCreateChild(cityRoot.transform, "RoadsRoot").transform;
            Transform blocksRoot = GetOrCreateChild(cityRoot.transform, "BlocksRoot").transform;
            Transform buildingsRoot = GetOrCreateChild(cityRoot.transform, "BuildingsRoot").transform;
            Transform poiRoot = GetOrCreateChild(cityRoot.transform, "POIRoot").transform;
            Transform sectorThemes = GetOrCreateChild(cityRoot.transform, "SectorThemes").transform;
            Transform regionGates = GetOrCreateChild(cityRoot.transform, "RegionGates").transform;
            Transform signalTemplateRoot = GetOrCreateChild(cityRoot.transform, "SignalTemplateRoot").transform;

            ClearChildren(roadsRoot);
            ClearChildren(blocksRoot);
            ClearChildren(buildingsRoot);
            ClearChildren(poiRoot);
            ClearChildren(sectorThemes);
            ClearChildren(regionGates);
            ClearChildren(signalTemplateRoot);

            List<string> buildingAssets = CollectBuildingAssets();
            Dictionary<string, List<string>> regionBuildingPools = BuildRegionBuildingPools(buildingAssets);
            var regionInfos = new List<RegionRuntimeInfo>(RegionWorldLayout.Count);
            var buildings = new List<BuildingEntry>(1024);
            var buildingMaterialCache = new Dictionary<string, Material>(256);
            int roadCount = 0;
            int blockCount = 0;
            int curbCount = 0;
            int buildCount = 0;
            int placeholderCount = 0;
            int globalIndex = 0;

            for (int i = 0; i < RegionWorldLayout.Count; i++)
            {
                RegionLayoutDefinition def;
                if (!RegionWorldLayout.TryGetByIndex(i, out def))
                {
                    continue;
                }

                Transform rr = GetOrCreateChild(roadsRoot, "Region_" + def.RegionId).transform;
                Transform br = GetOrCreateChild(blocksRoot, "Region_" + def.RegionId).transform;
                Transform bir = GetOrCreateChild(buildingsRoot, "Region_" + def.RegionId).transform;
                ClearChildren(rr);
                ClearChildren(br);
                ClearChildren(bir);

                CreateSectorMarker(sectorThemes, def);

                RegionVisualTheme theme = GetTheme(def.RegionId);
                List<string> regionBuildingAssets = GetRegionBuildingPool(regionBuildingPools, def.RegionId, buildingAssets);
                List<string> regionSkylineAssets = FilterAssetsByKeyword(regionBuildingAssets, "skyscraper", true);
                List<string> regionLowriseAssets = FilterAssetsByKeyword(regionBuildingAssets, "skyscraper", false);
                Material roadMat = EnsureRegionSurfaceMaterial(
                    def.RegionId,
                    "Road",
                    theme.RoadColor,
                    0.08f,
                    theme.RoadSmoothness);
                Material blockMat = EnsureRegionSurfaceMaterial(
                    def.RegionId,
                    "Block",
                    theme.BlockColor,
                    0.00f,
                    theme.BlockSmoothness);
                Material curbMat = EnsureRegionSurfaceMaterial(
                    def.RegionId,
                    "Curb",
                    Color.Lerp(theme.BlockColor, theme.RoadColor, 0.33f),
                    0.03f,
                    Mathf.Clamp01(theme.BlockSmoothness * 0.7f));

                float spanX;
                float spanZ;
                roadCount += BuildRegionRoads(rr, def, roadMat, out spanX, out spanZ);
                blockCount += BuildRegionBlocks(br, def, theme, blockMat);
                curbCount += BuildRegionCurbs(br, def, theme, curbMat);
                buildCount += BuildRegionBuildings(
                    bir,
                    def,
                    theme,
                    regionBuildingAssets,
                    regionSkylineAssets,
                    regionLowriseAssets,
                    buildingMaterialCache,
                    buildings,
                    ref placeholderCount,
                    ref globalIndex);
                regionInfos.Add(new RegionRuntimeInfo { Def = def, SpanX = spanX, SpanZ = spanZ });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    "[MegaWorldGenerator] Region " + def.RegionId +
                    " buildingPool=" + regionBuildingAssets.Count +
                    " skyline=" + regionSkylineAssets.Count +
                    " lowrise=" + regionLowriseAssets.Count);
#endif
            }

            roadCount += BuildConnectors(roadsRoot, regionInfos, connectorRoadMat);
            BuildRegionGates(regionGates, regionInfos, barrierMat);
            CreateTrafficSignalTemplate(signalTemplateRoot);
            AssignAnchors(buildings, poiRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MegaWorldGenerator] Scene: " + scenePath);
            Debug.Log("[MegaWorldGenerator] Cleanup removed: duplicateCityRoots=" + removedDuplicateCityRoots + " legacyRoots=" + removedLegacyRoots + " transient=" + removedTransient);
            Debug.Log("[MegaWorldGenerator] Regions=" + regionInfos.Count + " Roads=" + roadCount + " Blocks=" + blockCount + " Curbs=" + curbCount + " Buildings=" + buildCount + " Placeholder=" + placeholderCount);
        }

        private static int BuildRegionRoads(Transform root, RegionLayoutDefinition def, Material mat, out float spanX, out float spanZ)
        {
            spanX = (def.BlocksX * BlockSize) + ((def.BlocksX + 1) * RoadWidth);
            spanZ = (def.BlocksZ * BlockSize) + ((def.BlocksZ + 1) * RoadWidth);
            float halfX = spanX * 0.5f;
            float halfZ = spanZ * 0.5f;
            float step = BlockSize + RoadWidth;
            float startX = -halfX + (RoadWidth * 0.5f);
            float startZ = -halfZ + (RoadWidth * 0.5f);
            int count = 0;

            for (int x = 0; x <= def.BlocksX; x++)
            {
                float lx = startX + (x * step);
                CreateRoad(root, "Road_V_" + x.ToString("00"), new Vector3(def.Center.x + lx, -RoadHeight * 0.5f, def.Center.y), new Vector3(RoadWidth, RoadHeight, spanZ), mat);
                count++;
            }

            for (int z = 0; z <= def.BlocksZ; z++)
            {
                float lz = startZ + (z * step);
                CreateRoad(root, "Road_H_" + z.ToString("00"), new Vector3(def.Center.x, -RoadHeight * 0.5f, def.Center.y + lz), new Vector3(spanX, RoadHeight, RoadWidth), mat);
                count++;
            }

            Transform visualRoot = GetOrCreateChild(root, "KitVisuals").transform;
            ClearChildren(visualRoot);
            BuildRegionRoadVisuals(visualRoot, def, spanX, spanZ);

            return count;
        }

        private static int BuildRegionRoadVisuals(Transform root, RegionLayoutDefinition def, float spanX, float spanZ)
        {
            if (root == null || !s_roadKitMetricsReady || s_roadSkinStraightPrefab == null)
            {
                return 0;
            }

            int created = 0;
            Transform propsRoot = GetOrCreateChild(root, "RoadProps").transform;
            ClearChildren(propsRoot);
            float halfX = spanX * 0.5f;
            float halfZ = spanZ * 0.5f;
            float step = BlockSize + RoadWidth;
            float startX = -halfX + (RoadWidth * 0.5f);
            float startZ = -halfZ + (RoadWidth * 0.5f);
            int nodesX = def.BlocksX + 1;
            int nodesZ = def.BlocksZ + 1;
            float nodeHalfX = Mathf.Max(0.35f, (s_junctionWorldSizeX * 0.5f) - JunctionLaneJoinOverlap);
            float nodeHalfZ = Mathf.Max(0.35f, (s_junctionWorldSizeZ * 0.5f) - JunctionLaneJoinOverlap);

            var nodePositions = new Vector3[nodesX, nodesZ];
            for (int z = 0; z < nodesZ; z++)
            {
                for (int x = 0; x < nodesX; x++)
                {
                    Vector3 nodePos = new Vector3(
                        def.Center.x + startX + (x * step),
                        -RoadHeight * 0.5f,
                        def.Center.y + startZ + (z * step));
                    nodePositions[x, z] = nodePos;

                    bool north = z < nodesZ - 1;
                    bool east = x < nodesX - 1;
                    bool south = z > 0;
                    bool west = x > 0;
                    created += CreateRoadJunctionVisual(root, propsRoot, "Junction_" + x.ToString("00") + "_" + z.ToString("00"), nodePos, north, east, south, west);
                }
            }

            for (int z = 0; z < nodesZ; z++)
            {
                for (int x = 0; x < nodesX - 1; x++)
                {
                    Vector3 from = nodePositions[x, z] + new Vector3(nodeHalfX, 0f, 0f);
                    Vector3 to = nodePositions[x + 1, z] - new Vector3(nodeHalfX, 0f, 0f);
                    created += CreateRoadVisualStraightChain(root, "LaneH_" + z.ToString("00") + "_" + x.ToString("00"), from, to);
                }
            }

            for (int x = 0; x < nodesX; x++)
            {
                for (int z = 0; z < nodesZ - 1; z++)
                {
                    Vector3 from = nodePositions[x, z] + new Vector3(0f, 0f, nodeHalfZ);
                    Vector3 to = nodePositions[x, z + 1] - new Vector3(0f, 0f, nodeHalfZ);
                    created += CreateRoadVisualStraightChain(root, "LaneV_" + x.ToString("00") + "_" + z.ToString("00"), from, to);
                }
            }

            return created;
        }

        private static int CreateRoadJunctionVisual(
            Transform root,
            Transform propsRoot,
            string name,
            Vector3 center,
            bool north,
            bool east,
            bool south,
            bool west)
        {
            int degree = 0;
            if (north) degree++;
            if (east) degree++;
            if (south) degree++;
            if (west) degree++;

            GameObject prefab = s_roadSkinSquarePrefab != null ? s_roadSkinSquarePrefab : s_roadSkinStraightPrefab;
            float yaw = 0f;
            string finalName = name;
            float localZScaleRatio = 1f;
            float localForwardOffset = 0f;
            float localRightOffset = 0f;
            float visualScale = s_roadVisualUniformScale * JunctionVisualScaleNudge;

            if (degree >= 4)
            {
                prefab = s_roadSkinCrossroadPrefab != null ? s_roadSkinCrossroadPrefab : prefab;
                yaw = CrossroadYawOffset;
                finalName = "JunctionCross_" + name;
            }
            else if (degree == 3)
            {
                prefab = s_roadSkinTJunctionPrefab != null ? s_roadSkinTJunctionPrefab : (s_roadSkinCrossroadPrefab != null ? s_roadSkinCrossroadPrefab : prefab);
                CardinalDirection missing = CardinalDirection.North;
                if (!north) missing = CardinalDirection.North;
                else if (!east) missing = CardinalDirection.East;
                else if (!south) missing = CardinalDirection.South;
                else if (!west) missing = CardinalDirection.West;
                yaw = GetTJunctionYaw(missing);
                finalName = "JunctionT_" + name;
            }
            else if (degree == 2)
            {
                bool opposite = (north && south) || (east && west);
                if (opposite)
                {
                    prefab = s_roadSkinStraightPrefab != null ? s_roadSkinStraightPrefab : prefab;
                    yaw = (east && west) ? GetStraightYaw(Vector3.right) : GetStraightYaw(Vector3.forward);
                    finalName = "JunctionStraight_" + name;
                }
                else
                {
                    prefab = s_roadSkinCornerPrefab != null ? s_roadSkinCornerPrefab : prefab;
                    yaw = GetCornerYaw(north, east, south, west);
                    finalName = "JunctionCorner_" + name;
                    localForwardOffset = CornerDiagonalOffset;
                    localRightOffset = CornerDiagonalOffset;
                    visualScale *= CornerVisualScaleNudge;
                }
            }
            else if (degree == 1)
            {
                prefab = s_roadSkinEndPrefab != null ? s_roadSkinEndPrefab : (s_roadSkinStraightPrefab != null ? s_roadSkinStraightPrefab : prefab);
                CardinalDirection connected = GetConnectedDirection(north, east, south, west);
                yaw = GetEndYaw(connected);
                finalName = "JunctionEnd_" + name;
            }
            else
            {
                finalName = "JunctionSquare_" + name;
            }

            if (degree == 3)
            {
                localZScaleRatio = TJunctionLocalZScaleRatio;
                localForwardOffset = TJunctionForwardOffset;
            }

            if (!TryCreateRoadVisual(root, prefab, finalName, center, yaw, visualScale, localZScaleRatio, localForwardOffset, localRightOffset))
            {
                return 0;
            }

            CreateIntersectionSignals(propsRoot, finalName, center, north, east, south, west, degree);
            CreateIntersectionStreetLights(propsRoot, finalName, center, north, east, south, west, degree);
            return 1;
        }

        private static int CreateRoadVisualStraightChain(Transform root, string baseName, Vector3 from, Vector3 to)
        {
            if (root == null || s_roadSkinStraightPrefab == null)
            {
                return 0;
            }

            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist <= 0.2f)
            {
                return 0;
            }

            Vector3 dir = delta / dist;
            float defaultLen = Mathf.Max(0.5f, s_straightSourceLength * s_roadVisualUniformScale);
            int pieceCount = Mathf.Max(1, Mathf.RoundToInt(dist / defaultLen));
            float pieceLen = dist / pieceCount;
            float pieceScale = Mathf.Max(0.05f, pieceLen / Mathf.Max(0.001f, s_straightSourceLength));
            pieceScale *= StraightVisualScaleNudge;
            float yaw = GetStraightYaw(dir);
            int created = 0;

            for (int i = 0; i < pieceCount; i++)
            {
                Vector3 center = from + (dir * (pieceLen * (i + 0.5f)));
                if (TryCreateRoadVisual(root, s_roadSkinStraightPrefab, baseName + "_S_" + i.ToString("00"), center, yaw, pieceScale))
                {
                    created++;
                }
            }

            return created;
        }

        private static int CreateIntersectionSignals(
            Transform root,
            string baseName,
            Vector3 center,
            bool north,
            bool east,
            bool south,
            bool west,
            int degree)
        {
            if (root == null || degree != 4)
            {
                return 0;
            }

            GameObject signalPrefab =
                s_signalSkinSinglePrefab != null
                    ? s_signalSkinSinglePrefab
                    : (s_signalSkinPrefab != null ? s_signalSkinPrefab : s_signalSkinDoublePrefab);
            if (signalPrefab == null)
            {
                return 0;
            }

            float halfRoad = Mathf.Max(1.0f, Mathf.Min(s_junctionWorldSizeX, s_junctionWorldSizeZ) * 0.5f);
            float offset = Mathf.Min(JunctionSignalOffset, Mathf.Max(4.0f, halfRoad * 0.72f));

            int created = 0;
            int index = 0;

            if (north)
            {
                Vector3 pos = center + new Vector3(0f, 0f, offset);
                if (TryCreateRoadPropVisual(root, signalPrefab, "Signal_" + baseName + "_" + index.ToString("00"), pos, 180f))
                {
                    created++;
                }

                index++;
            }

            if (east)
            {
                Vector3 pos = center + new Vector3(offset, 0f, 0f);
                if (TryCreateRoadPropVisual(root, signalPrefab, "Signal_" + baseName + "_" + index.ToString("00"), pos, 270f))
                {
                    created++;
                }

                index++;
            }

            if (south)
            {
                Vector3 pos = center + new Vector3(0f, 0f, -offset);
                if (TryCreateRoadPropVisual(root, signalPrefab, "Signal_" + baseName + "_" + index.ToString("00"), pos, 0f))
                {
                    created++;
                }

                index++;
            }

            if (west)
            {
                Vector3 pos = center + new Vector3(-offset, 0f, 0f);
                if (TryCreateRoadPropVisual(root, signalPrefab, "Signal_" + baseName + "_" + index.ToString("00"), pos, 90f))
                {
                    created++;
                }

                index++;
            }

            return created;
        }

        private static int CreateIntersectionStreetLights(
            Transform root,
            string baseName,
            Vector3 center,
            bool north,
            bool east,
            bool south,
            bool west,
            int degree)
        {
            if (root == null || degree != 3)
            {
                return 0;
            }

            GameObject streetLightPrefab =
                s_streetLightSkinPrefab != null
                    ? s_streetLightSkinPrefab
                    : (s_streetLightSkinDoublePrefab != null ? s_streetLightSkinDoublePrefab : s_streetLightSkinCrossPrefab);
            if (streetLightPrefab == null)
            {
                return 0;
            }

            float halfRoad = Mathf.Max(1.0f, Mathf.Min(s_junctionWorldSizeX, s_junctionWorldSizeZ) * 0.5f);
            float offset = Mathf.Max(JunctionStreetLightOffset, halfRoad + 1.75f);
            CardinalDirection missing = GetMissingDirection(north, east, south, west);

            int created = 0;
            int index = 0;

            Action<float, float, float> place = (dx, dz, yaw) =>
            {
                Vector3 pos = center + new Vector3(dx, 0f, dz);
                if (TryCreateRoadPropVisual(root, streetLightPrefab, "StreetLight_" + baseName + "_" + index.ToString("00"), pos, yaw))
                {
                    created++;
                }

                index++;
            };

            switch (missing)
            {
                case CardinalDirection.North:
                    place(offset, offset, 225f);
                    place(-offset, offset, 135f);
                    break;
                case CardinalDirection.East:
                    place(offset, offset, 225f);
                    place(offset, -offset, 315f);
                    break;
                case CardinalDirection.South:
                    place(offset, -offset, 315f);
                    place(-offset, -offset, 45f);
                    break;
                case CardinalDirection.West:
                    place(-offset, offset, 135f);
                    place(-offset, -offset, 45f);
                    break;
                default:
                    place(offset, offset, 225f);
                    place(-offset, offset, 135f);
                    break;
            }

            return created;
        }

        private static bool TryCreateRoadPropVisual(Transform parent, GameObject prefab, string name, Vector3 logicalRoadCenter, float yawDeg)
        {
            if (parent == null || prefab == null)
            {
                return false;
            }

            GameObject prop = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (prop == null)
            {
                prop = Object.Instantiate(prefab, parent);
            }

            if (prop == null)
            {
                return false;
            }

            prop.name = name;
            prop.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            prop.transform.localScale = Vector3.one;
            prop.transform.position = logicalRoadCenter;

            RemoveAllRigidbodies(prop);
            RemoveAllColliders(prop);

            Bounds bounds;
            if (TryGetRendererBounds(prop, out bounds))
            {
                float dx = logicalRoadCenter.x - bounds.center.x;
                float dz = logicalRoadCenter.z - bounds.center.z;
                prop.transform.position += new Vector3(dx, 0f, dz);

                if (TryGetRendererBounds(prop, out bounds))
                {
                    float roadTopY = logicalRoadCenter.y + (RoadHeight * 0.5f);
                    float yDelta = (roadTopY + RoadSkinYOffset) - bounds.min.y;
                    prop.transform.position += new Vector3(0f, yDelta, 0f);
                }
            }

            SetGeneratedStatic(prop);
            return true;
        }

        private static float GetStraightYaw(Vector3 direction)
        {
            bool alongX = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            if (alongX)
            {
                if (s_straightLongAxisX)
                {
                    return direction.x >= 0f ? 0f : 180f;
                }

                return direction.x >= 0f ? 90f : 270f;
            }

            if (s_straightLongAxisX)
            {
                return direction.z >= 0f ? 90f : 270f;
            }

            return direction.z >= 0f ? 0f : 180f;
        }

        private static float GetTJunctionYaw(CardinalDirection missingDirection)
        {
            switch (missingDirection)
            {
                case CardinalDirection.North:
                    return 180f;
                case CardinalDirection.East:
                    return 270f;
                case CardinalDirection.South:
                    return 0f;
                case CardinalDirection.West:
                    return 90f;
                default:
                    return 180f;
            }
        }

        private static float GetCornerYaw(bool north, bool east, bool south, bool west)
        {
            if (north && east) return 0f;
            if (west && north) return 90f;
            if (south && west) return 180f;
            if (east && south) return 270f;
            return 0f;
        }

        private static CardinalDirection GetConnectedDirection(bool north, bool east, bool south, bool west)
        {
            if (north) return CardinalDirection.North;
            if (east) return CardinalDirection.East;
            if (south) return CardinalDirection.South;
            if (west) return CardinalDirection.West;
            return CardinalDirection.North;
        }

        private static CardinalDirection GetMissingDirection(bool north, bool east, bool south, bool west)
        {
            if (!north) return CardinalDirection.North;
            if (!east) return CardinalDirection.East;
            if (!south) return CardinalDirection.South;
            if (!west) return CardinalDirection.West;
            return CardinalDirection.North;
        }

        private static float GetEndYaw(CardinalDirection connectedDirection)
        {
            switch (connectedDirection)
            {
                case CardinalDirection.North:
                    return 180f;
                case CardinalDirection.East:
                    return 270f;
                case CardinalDirection.South:
                    return 0f;
                case CardinalDirection.West:
                    return 90f;
                default:
                    return 0f;
            }
        }

        private static int BuildRegionBlocks(Transform root, RegionLayoutDefinition def, RegionVisualTheme theme, Material mat)
        {
            // Keep block-generation hook for call-site compatibility, but stop creating legacy Block_### visuals.
            _ = root;
            _ = def;
            _ = theme;
            _ = mat;
            return 0;
        }

        private static void CreateBlockFoundation(Transform blockRoot, float blockHeight, Material blockMaterial)
        {
            if (blockRoot == null)
            {
                return;
            }

            float foundationDepth = Mathf.Max(1.5f, BlockFoundationDepth);
            GameObject foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foundation.name = "Foundation";
            foundation.transform.SetParent(blockRoot, false);
            // Keep foundation top clearly below road top to prevent z-fighting at block edges.
            foundation.transform.localPosition = new Vector3(0f, -(foundationDepth * 0.5f + blockHeight * 0.5f + BlockFoundationTopOffset), 0f);
            foundation.transform.localRotation = Quaternion.identity;
            foundation.transform.localScale = new Vector3(BlockSize * 0.98f, foundationDepth, BlockSize * 0.98f);

            Renderer renderer = foundation.GetComponent<Renderer>();
            if (renderer != null && blockMaterial != null)
            {
                renderer.sharedMaterial = blockMaterial;
            }

            SetGeneratedStatic(foundation);
        }

        private static void CreateBlockEdgeColliders(Transform blockTransform, float blockSize, float blockHeight)
        {
            if (blockTransform == null)
            {
                return;
            }

            float halfBlock = blockSize * 0.5f;
            float halfThickness = BlockEdgeWallThickness * 0.5f;
            float sideLength = blockSize + (BlockEdgeWallExtend * 2f);
            float wallHeight = Mathf.Max(BlockEdgeWallHeight, blockHeight + 0.85f);
            float localY = (wallHeight * 0.5f) - (blockHeight * 0.5f);

            CreateBlockEdgeCollider(
                blockTransform,
                "EdgeNorth",
                new Vector3(0f, localY, halfBlock + halfThickness),
                new Vector3(sideLength, wallHeight, BlockEdgeWallThickness));
            CreateBlockEdgeCollider(
                blockTransform,
                "EdgeSouth",
                new Vector3(0f, localY, -(halfBlock + halfThickness)),
                new Vector3(sideLength, wallHeight, BlockEdgeWallThickness));
            CreateBlockEdgeCollider(
                blockTransform,
                "EdgeEast",
                new Vector3(halfBlock + halfThickness, localY, 0f),
                new Vector3(BlockEdgeWallThickness, wallHeight, sideLength));
            CreateBlockEdgeCollider(
                blockTransform,
                "EdgeWest",
                new Vector3(-(halfBlock + halfThickness), localY, 0f),
                new Vector3(BlockEdgeWallThickness, wallHeight, sideLength));
        }

        private static void CreateBlockEdgeCollider(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 colliderSize)
        {
            GameObject edge = new GameObject(name);
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = localPosition;
            edge.transform.localRotation = Quaternion.identity;

            BoxCollider collider = edge.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = colliderSize;
            collider.isTrigger = false;
            SetGeneratedStatic(edge);
        }

        private static int BuildRegionCurbs(Transform root, RegionLayoutDefinition def, RegionVisualTheme theme, Material mat)
        {
            int idx = 0;
            float step = BlockSize + RoadWidth;
            float sx = -((def.BlocksX - 1) * step * 0.5f);
            float sz = -((def.BlocksZ - 1) * step * 0.5f);
            float blockHeight = Mathf.Max(0.18f, BlockHeight * theme.BlockHeightMul);
            float curbHeight = Mathf.Clamp(0.08f * theme.BlockHeightMul, 0.05f, 0.14f);
            float curbTopY = Mathf.Max(blockHeight + curbHeight, CurbBarrierTopMinY);
            float curbVisualHeight = Mathf.Max(0.5f, curbTopY - CurbBarrierBottomY);
            float curbY = CurbBarrierBottomY + (curbVisualHeight * 0.5f);
            float curbSize = BlockSize + 1.2f;
            for (int z = 0; z < def.BlocksZ; z++)
            {
                for (int x = 0; x < def.BlocksX; x++)
                {
                    GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    curb.name = "BlockCurb_" + idx.ToString("000");
                    curb.transform.SetParent(root, false);
                    curb.transform.position = new Vector3(def.Center.x + sx + (x * step), curbY, def.Center.y + sz + (z * step));
                    curb.transform.localScale = new Vector3(curbSize, curbVisualHeight, curbSize);
                    Renderer renderer = curb.GetComponent<Renderer>();
                    if (renderer != null && mat != null)
                    {
                        renderer.sharedMaterial = mat;
                    }

                    Collider collider = curb.GetComponent<Collider>();
                    if (collider != null && !(collider is BoxCollider))
                    {
                        Object.DestroyImmediate(collider);
                    }

                    ConfigureCurbSolidCollider(curb);

                    SetGeneratedStatic(curb);
                    idx++;
                }
            }

            return idx;
        }

        private static int BuildRegionBuildings(
            Transform root,
            RegionLayoutDefinition def,
            RegionVisualTheme theme,
            List<string> assets,
            List<string> skylineAssets,
            List<string> lowriseAssets,
            Dictionary<string, Material> buildingMaterialCache,
            List<BuildingEntry> outList,
            ref int placeholderCount,
            ref int globalIndex)
        {
            int created = 0;
            int regionOrder = 0;
            float step = BlockSize + RoadWidth;
            float sx = -((def.BlocksX - 1) * step * 0.5f);
            float sz = -((def.BlocksZ - 1) * step * 0.5f);
            float blockHeight = Mathf.Max(0.18f, BlockHeight * theme.BlockHeightMul);
            float topY = blockHeight;
            float lotInset = Mathf.Clamp(LotInset * theme.LotInsetMul, 7.2f, 13.8f);
            float[] lots = { -lotInset, 0f, lotInset };

            for (int z = 0; z < def.BlocksZ; z++)
            {
                for (int x = 0; x < def.BlocksX; x++)
                {
                    Vector3 center = new Vector3(def.Center.x + sx + (x * step), topY, def.Center.y + sz + (z * step));
                    for (int lz = 0; lz < 3; lz++)
                    {
                        for (int lx = 0; lx < 3; lx++)
                        {
                            if (lx == 1 && lz == 1) continue;

                            GameObject go = InstantiateBuildingForTheme(
                                assets,
                                skylineAssets,
                                lowriseAssets,
                                theme,
                                globalIndex,
                                regionOrder,
                                root);
                            if (go == null)
                            {
                                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                go.name = "Building_Placeholder_" + globalIndex.ToString("000");
                                go.transform.SetParent(root, false);
                                go.transform.localScale = new Vector3(12f, 9f, 12f);
                                placeholderCount++;
                            }

                            Vector3 lotPosition = new Vector3(center.x + lots[lx], center.y, center.z + lots[lz]);
                            PositionBuildingOnLot(go, lotPosition, globalIndex, theme);

                            Rigidbody[] rbs = go.GetComponentsInChildren<Rigidbody>(true);
                            for (int i = 0; i < rbs.Length; i++) Object.DestroyImmediate(rbs[i]);
                            RemoveAllColliders(go);
                            ApplyBuildingTheme(go, def.RegionId, theme, buildingMaterialCache);
                            OptimizeBuildingRenderers(go);
                            SetGeneratedStatic(go);

                            outList.Add(new BuildingEntry { Go = go, RegionId = def.RegionId, RegionOrder = regionOrder });
                            created++;
                            regionOrder++;
                            globalIndex++;
                        }
                    }
                }
            }

            return created;
        }

        private static void PositionBuildingOnLot(GameObject building, Vector3 lotPosition, int lotIndex, RegionVisualTheme theme)
        {
            if (building == null)
            {
                return;
            }

            float seedBase = (lotIndex + 1) * 1.731f + theme.SeedOffset;
            float jitterRange = Mathf.Max(0.15f, theme.BuildingJitterMeters);
            float yawRange = Mathf.Max(2f, theme.RotationJitterDeg);
            float jitterX = DeterministicRange(seedBase, -jitterRange, jitterRange);
            float jitterZ = DeterministicRange(seedBase + 3.29f, -jitterRange, jitterRange);
            float yawJitter = DeterministicRange(seedBase + 6.11f, -yawRange, yawRange);
            float footprintMul = DeterministicRange(seedBase + 9.97f, 0.92f, 1.10f) * Mathf.Max(0.75f, theme.BuildingFootprintMul);
            float heightMul = DeterministicRange(seedBase + 12.43f, 0.95f, 1.18f) * Mathf.Max(0.75f, theme.BuildingHeightMul);

            Vector3 adjustedLot = new Vector3(lotPosition.x + jitterX, lotPosition.y, lotPosition.z + jitterZ);
            float targetFootprint = (12.2f + ((lotIndex % 3) * 1.1f)) * Mathf.Clamp(footprintMul, 0.72f, 1.35f);
            float minHeight = BuildingMinHeightBase * Mathf.Clamp(heightMul, 0.68f, 1.08f);
            float maxHeight = BuildingMaxHeightBase * Mathf.Clamp(heightMul, 0.72f, 1.05f);
            FitBuildingToLot(building, targetFootprint, minHeight, maxHeight);

            float y = ComputePlacementY(building, adjustedLot.y);
            building.transform.position = new Vector3(adjustedLot.x, y, adjustedLot.z);
            building.transform.rotation = Quaternion.Euler(0f, (lotIndex % 4) * 90f + yawJitter, 0f);
        }

        private static void FitBuildingToLot(GameObject building, float targetFootprint, float minHeight, float maxHeight)
        {
            Bounds bounds;
            if (!TryGetRendererBounds(building, out bounds))
            {
                return;
            }

            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint > 0.0001f)
            {
                float uniformScale = Mathf.Clamp(targetFootprint / footprint, 0.20f, 24f);
                building.transform.localScale *= uniformScale;
            }

            if (!TryGetRendererBounds(building, out bounds))
            {
                return;
            }

            float height = Mathf.Max(0.0001f, bounds.size.y);
            if (height < minHeight)
            {
                float yScaleMul = Mathf.Clamp(minHeight / height, 1f, 4f);
                Vector3 localScale = building.transform.localScale;
                localScale.y *= yScaleMul;
                building.transform.localScale = localScale;
            }

            if (!TryGetRendererBounds(building, out bounds))
            {
                return;
            }

            height = Mathf.Max(0.0001f, bounds.size.y);
            if (height > maxHeight)
            {
                float yScaleMul = Mathf.Clamp(maxHeight / height, 0.35f, 1f);
                Vector3 localScale = building.transform.localScale;
                localScale.y *= yScaleMul;
                building.transform.localScale = localScale;
            }
        }

        private static float ComputePlacementY(GameObject go, float blockTopY)
        {
            Bounds bounds;
            if (!TryGetRendererBounds(go, out bounds))
            {
                return blockTopY + 1f;
            }

            float minOffsetFromPivot = bounds.min.y - go.transform.position.y;
            return blockTopY - minOffsetFromPivot + 0.02f;
        }

        private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static int BuildConnectors(Transform roadsRoot, List<RegionRuntimeInfo> regions, Material mat)
        {
            int created = 0;
            Transform visualRoot = GetOrCreateChild(roadsRoot, "ConnectorVisuals").transform;
            ClearChildren(visualRoot);
            for (int i = 0; i < RegionConnections.Length; i++)
            {
                RegionConnectionDefinition link = RegionConnections[i];
                created += CreateConnectorIfSeparated(roadsRoot, visualRoot, regions, link.FromRegionId, link.ToRegionId, link.ConnectorName, mat);
            }

            return created;
        }

        private static int CreateConnectorIfSeparated(
            Transform roadsRoot,
            Transform visualRoot,
            List<RegionRuntimeInfo> regions,
            string fromId,
            string toId,
            string name,
            Material mat)
        {
            RegionRuntimeInfo from = default;
            RegionRuntimeInfo to = default;
            bool hasFrom = false;
            bool hasTo = false;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Def.RegionId == fromId) { from = regions[i]; hasFrom = true; }
                if (regions[i].Def.RegionId == toId) { to = regions[i]; hasTo = true; }
            }
            if (!hasFrom || !hasTo) return 0;

            Vector2 a = from.Def.Center;
            Vector2 b = to.Def.Center;
            float dx = b.x - a.x;
            float dz = b.y - a.y;
            float absDx = Mathf.Abs(dx);
            float absDz = Mathf.Abs(dz);
            if (Mathf.Abs(dx) >= Mathf.Abs(dz))
            {
                float halfFrom = from.SpanX * 0.5f;
                float halfTo = to.SpanX * 0.5f;
                float gap = absDx - (halfFrom + halfTo);
                if (gap <= 0.1f)
                {
                    return 0;
                }

                float sign = dx >= 0f ? 1f : -1f;
                float fromX = a.x + (sign * halfFrom);
                float toX = b.x - (sign * halfTo);
                float z = (a.y + b.y) * 0.5f;
                float width = Mathf.Max(0.2f, Mathf.Abs(toX - fromX));
                CreateRoad(roadsRoot, name, new Vector3((fromX + toX) * 0.5f, -RoadHeight * 0.5f, z), new Vector3(width, RoadHeight, RoadWidth), mat);
                CreateRoadVisualStraightChain(
                    visualRoot,
                    "ConnectorVisual_" + name,
                    new Vector3(fromX, -RoadHeight * 0.5f, z),
                    new Vector3(toX, -RoadHeight * 0.5f, z));
            }
            else
            {
                float halfFrom = from.SpanZ * 0.5f;
                float halfTo = to.SpanZ * 0.5f;
                float gap = absDz - (halfFrom + halfTo);
                if (gap <= 0.1f)
                {
                    return 0;
                }

                float sign = dz >= 0f ? 1f : -1f;
                float fromZ = a.y + (sign * halfFrom);
                float toZ = b.y - (sign * halfTo);
                float x = (a.x + b.x) * 0.5f;
                float length = Mathf.Max(0.2f, Mathf.Abs(toZ - fromZ));
                CreateRoad(roadsRoot, name, new Vector3(x, -RoadHeight * 0.5f, (fromZ + toZ) * 0.5f), new Vector3(RoadWidth, RoadHeight, length), mat);
                CreateRoadVisualStraightChain(
                    visualRoot,
                    "ConnectorVisual_" + name,
                    new Vector3(x, -RoadHeight * 0.5f, fromZ),
                    new Vector3(x, -RoadHeight * 0.5f, toZ));
            }
            return 1;
        }

        private static void CreateRoad(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = name;
            road.transform.SetParent(parent, false);
            road.transform.position = pos;
            road.transform.localScale = scale;

            Renderer renderer = road.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (mat != null)
                {
                    renderer.sharedMaterial = mat;
                }

                // Keep gameplay collider/road-surface object, hide legacy stretched cube visual.
                renderer.enabled = false;
            }

            ConfigureRoadCollider(road, pos, scale);
            if (road.GetComponent<RoadSurface>() == null)
            {
                road.AddComponent<RoadSurface>();
            }

            SetGeneratedStatic(road);
        }

        private static bool TryCreateRoadVisual(
            Transform parent,
            GameObject prefab,
            string name,
            Vector3 logicalRoadCenter,
            float yawDeg,
            float uniformScale,
            float localZScaleRatio = 1f,
            float localForwardOffset = 0f,
            float localRightOffset = 0f)
        {
            if (parent == null || prefab == null)
            {
                return false;
            }

            GameObject visual = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (visual == null)
            {
                visual = Object.Instantiate(prefab, parent);
            }

            if (visual == null)
            {
                return false;
            }

            visual.name = name;
            visual.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            float safeScale = Mathf.Max(0.001f, uniformScale);
            float safeZRatio = Mathf.Max(0.001f, localZScaleRatio);
            visual.transform.localScale = new Vector3(safeScale, safeScale, safeScale * safeZRatio);
            visual.transform.position = logicalRoadCenter;

            RemoveAllRigidbodies(visual);
            RemoveAllColliders(visual);

            Bounds bounds;
            if (TryGetRendererBounds(visual, out bounds))
            {
                float dx = logicalRoadCenter.x - bounds.center.x;
                float dz = logicalRoadCenter.z - bounds.center.z;
                visual.transform.position += new Vector3(dx, 0f, dz);

                if (TryGetRendererBounds(visual, out bounds))
                {
                    float roadTopY = logicalRoadCenter.y + (RoadHeight * 0.5f);
                    float yDelta = (roadTopY + RoadSkinYOffset) - bounds.min.y;
                    visual.transform.position += new Vector3(0f, yDelta, 0f);
                }
            }

            if (Mathf.Abs(localForwardOffset) > 0.0001f)
            {
                visual.transform.position += visual.transform.forward * localForwardOffset;
            }

            if (Mathf.Abs(localRightOffset) > 0.0001f)
            {
                visual.transform.position += visual.transform.right * localRightOffset;
            }

            SetGeneratedStatic(visual);
            return true;
        }

        private static void CacheRoadKitMetrics()
        {
            s_roadKitMetricsReady = false;
            s_roadVisualUniformScale = 1f;
            s_straightSourceLength = 1f;
            s_straightSourceWidth = 1f;
            s_straightLongAxisX = true;
            s_junctionWorldSizeX = RoadWidth;
            s_junctionWorldSizeZ = RoadWidth;

            if (s_roadSkinStraightPrefab == null)
            {
                return;
            }

            float straightX;
            float straightZ;
            if (!TryMeasurePrefabFootprint(s_roadSkinStraightPrefab, out straightX, out straightZ))
            {
                s_straightSourceLength = 1f;
                s_straightSourceWidth = 1f;
                s_straightLongAxisX = true;
                s_roadVisualUniformScale = RoadWidth;
                s_junctionWorldSizeX = RoadWidth;
                s_junctionWorldSizeZ = RoadWidth;
                s_roadKitMetricsReady = true;
                return;
            }

            s_straightLongAxisX = straightX >= straightZ;
            s_straightSourceLength = Mathf.Max(0.001f, Mathf.Max(straightX, straightZ));
            s_straightSourceWidth = Mathf.Max(0.001f, Mathf.Min(straightX, straightZ));
            s_roadVisualUniformScale = Mathf.Max(0.01f, RoadWidth / s_straightSourceWidth);

            float junctionX;
            float junctionZ;
            GameObject junctionPrefab = s_roadSkinCrossroadPrefab != null
                ? s_roadSkinCrossroadPrefab
                : (s_roadSkinSquarePrefab != null ? s_roadSkinSquarePrefab : s_roadSkinStraightPrefab);
            if (TryMeasurePrefabFootprint(junctionPrefab, out junctionX, out junctionZ))
            {
                s_junctionWorldSizeX = Mathf.Max(0.2f, junctionX * s_roadVisualUniformScale);
                s_junctionWorldSizeZ = Mathf.Max(0.2f, junctionZ * s_roadVisualUniformScale);
            }

            s_roadKitMetricsReady = true;
        }

        private static bool TryMeasurePrefabFootprint(GameObject prefab, out float sizeX, out float sizeZ)
        {
            sizeX = 0f;
            sizeZ = 0f;
            if (prefab == null)
            {
                return false;
            }

            GameObject temp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (temp == null)
            {
                temp = Object.Instantiate(prefab);
            }

            if (temp == null)
            {
                return false;
            }

            bool ok = false;
            Bounds bounds;
            if (TryGetRendererBounds(temp, out bounds))
            {
                sizeX = Mathf.Max(0.001f, bounds.size.x);
                sizeZ = Mathf.Max(0.001f, bounds.size.z);
                ok = true;
            }

            Object.DestroyImmediate(temp);
            return ok;
        }

        private static void ConfigureRoadCollider(GameObject road, Vector3 roadCenter, Vector3 roadScale)
        {
            if (road == null)
            {
                return;
            }

            BoxCollider box = road.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = road.AddComponent<BoxCollider>();
            }

            Vector3 localScale = road.transform.localScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(localScale.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(localScale.y));
            float sz = Mathf.Max(0.0001f, Mathf.Abs(localScale.z));

            box.size = new Vector3(roadScale.x / sx, roadScale.y / sy, roadScale.z / sz);
            box.center = new Vector3(
                (roadCenter.x - road.transform.position.x) / localScale.x,
                (roadCenter.y - road.transform.position.y) / localScale.y,
                (roadCenter.z - road.transform.position.z) / localScale.z);
            box.isTrigger = false;
        }

        private static void CacheRoadSkinPrefabs()
        {
            s_roadSkinStraightPrefab = LoadKitPrefab(RoadSkinStraightPath, "road-straight t:GameObject");
            s_roadSkinSquarePrefab = LoadKitPrefab(RoadSkinSquarePath, "road-square t:GameObject");
            s_roadSkinCornerPrefab = LoadKitPrefab(RoadSkinCornerPath, "road-bend-sidewalk t:GameObject");
            if (s_roadSkinCornerPrefab == null)
            {
                s_roadSkinCornerPrefab = LoadKitPrefab(RoadSkinCornerFallbackPath, "road-curve-intersection t:GameObject");
            }

            if (s_roadSkinCornerPrefab == null)
            {
                s_roadSkinCornerPrefab = LoadKitPrefab(RoadSkinCornerSecondFallbackPath, "road-bend t:GameObject");
            }

            s_roadSkinTJunctionPrefab = LoadKitPrefab(RoadSkinTJunctionPath, "road-intersection t:GameObject");
            if (s_roadSkinTJunctionPrefab == null)
            {
                s_roadSkinTJunctionPrefab = LoadKitPrefab(RoadSkinTJunctionFallbackPath, "road-intersection-path t:GameObject");
            }
            s_roadSkinCrossroadPrefab = LoadKitPrefab(RoadSkinCrossroadPath, "road-crossroad-path t:GameObject");
            s_roadSkinEndPrefab = LoadKitPrefab(RoadSkinEndPath, "road-end t:GameObject");
            if (s_roadSkinStraightPrefab == null)
            {
                s_roadSkinStraightPrefab = s_roadSkinSquarePrefab;
            }

            if (s_roadSkinSquarePrefab == null)
            {
                s_roadSkinSquarePrefab = s_roadSkinStraightPrefab;
            }

            if (s_roadSkinCrossroadPrefab == null)
            {
                s_roadSkinCrossroadPrefab = s_roadSkinSquarePrefab;
            }

            if (s_roadSkinTJunctionPrefab == null)
            {
                s_roadSkinTJunctionPrefab = s_roadSkinCrossroadPrefab;
            }

            if (s_roadSkinCornerPrefab == null)
            {
                s_roadSkinCornerPrefab = s_roadSkinSquarePrefab;
            }

            if (s_roadSkinEndPrefab == null)
            {
                s_roadSkinEndPrefab = s_roadSkinStraightPrefab;
            }

            CacheRoadKitMetrics();
        }

        private static void CacheSignalSkinPrefab()
        {
            s_signalSkinSinglePrefab = LoadKitPrefab(SignalSkinSinglePath, "light-square t:GameObject");
            s_signalSkinPrefab = LoadKitPrefab(SignalSkinCrossPath, "light-square-cross t:GameObject");
            s_signalSkinDoublePrefab = LoadKitPrefab(SignalSkinDoublePath, "light-square-double t:GameObject");
            if (s_signalSkinSinglePrefab == null)
            {
                s_signalSkinSinglePrefab = s_signalSkinDoublePrefab != null ? s_signalSkinDoublePrefab : s_signalSkinPrefab;
            }

            if (s_signalSkinDoublePrefab == null)
            {
                s_signalSkinDoublePrefab = s_signalSkinSinglePrefab != null ? s_signalSkinSinglePrefab : s_signalSkinPrefab;
            }

            if (s_signalSkinPrefab == null)
            {
                s_signalSkinPrefab = s_signalSkinSinglePrefab != null ? s_signalSkinSinglePrefab : s_signalSkinDoublePrefab;
            }

            if (s_signalSkinSinglePrefab == null)
            {
                s_signalSkinSinglePrefab = s_signalSkinPrefab;
            }

            s_streetLightSkinPrefab = LoadKitPrefab(StreetLightSkinPrimaryPath, "light-curved t:GameObject");
            s_streetLightSkinCrossPrefab = LoadKitPrefab(StreetLightSkinCrossPath, "light-curved-cross t:GameObject");
            s_streetLightSkinDoublePrefab = LoadKitPrefab(StreetLightSkinDoublePath, "light-curved-double t:GameObject");

            if (s_streetLightSkinPrefab == null)
            {
                s_streetLightSkinPrefab = s_streetLightSkinDoublePrefab != null
                    ? s_streetLightSkinDoublePrefab
                    : s_streetLightSkinCrossPrefab;
            }

            if (s_streetLightSkinCrossPrefab == null)
            {
                s_streetLightSkinCrossPrefab = s_streetLightSkinDoublePrefab != null
                    ? s_streetLightSkinDoublePrefab
                    : s_streetLightSkinPrefab;
            }

            if (s_streetLightSkinDoublePrefab == null)
            {
                s_streetLightSkinDoublePrefab = s_streetLightSkinPrefab != null
                    ? s_streetLightSkinPrefab
                    : s_streetLightSkinCrossPrefab;
            }
        }

        private static GameObject LoadKitPrefab(string preferredPath, string searchFilter)
        {
            if (!string.IsNullOrEmpty(preferredPath))
            {
                GameObject preferred = AssetDatabase.LoadAssetAtPath<GameObject>(preferredPath);
                if (preferred != null)
                {
                    return preferred;
                }
            }

            string[] searchFolders = { RoadsKitModelRoot, RoadsKitRoot };
            string[] guids = AssetDatabase.FindAssets(searchFilter, searchFolders);
            for (int i = 0; guids != null && i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                GameObject found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void CreateTrafficSignalTemplate(Transform parent)
        {
            GameObject templatePrefab = s_signalSkinSinglePrefab != null ? s_signalSkinSinglePrefab : s_signalSkinPrefab;
            if (parent == null || templatePrefab == null)
            {
                return;
            }

            GameObject template = PrefabUtility.InstantiatePrefab(templatePrefab, parent) as GameObject;
            if (template == null)
            {
                template = Object.Instantiate(templatePrefab, parent);
            }

            if (template == null)
            {
                return;
            }

            template.name = "TrafficSignalTemplate";
            template.transform.localPosition = new Vector3(0f, -200f, 0f);
            template.transform.localRotation = Quaternion.identity;
            template.transform.localScale = Vector3.one;

            RemoveAllRigidbodies(template);
            RemoveAllColliders(template);
            SetGeneratedStatic(template);
        }

        private static void BuildRegionGates(Transform gatesRoot, List<RegionRuntimeInfo> regions, Material barrierMat)
        {
            int created = 0;
            for (int i = 0; i < RegionConnections.Length; i++)
            {
                RegionConnectionDefinition link = RegionConnections[i];
                if (CreateRegionGate(gatesRoot, regions, link, barrierMat))
                {
                    created++;
                }
            }

            Debug.Log("[MegaWorldGenerator] Region gates generated: " + created);
        }

        private static bool CreateRegionGate(
            Transform gatesRoot,
            List<RegionRuntimeInfo> regions,
            RegionConnectionDefinition link,
            Material barrierMat)
        {
            RegionRuntimeInfo from;
            RegionRuntimeInfo to;
            if (!TryFindRegionInfo(regions, link.FromRegionId, out from) ||
                !TryFindRegionInfo(regions, link.ToRegionId, out to))
            {
                return false;
            }

            Vector2 fromCenter = from.Def.Center;
            Vector2 toCenter = to.Def.Center;

            float fromMinX = fromCenter.x - (from.SpanX * 0.5f);
            float fromMaxX = fromCenter.x + (from.SpanX * 0.5f);
            float fromMinZ = fromCenter.y - (from.SpanZ * 0.5f);
            float fromMaxZ = fromCenter.y + (from.SpanZ * 0.5f);

            float toMinX = toCenter.x - (to.SpanX * 0.5f);
            float toMaxX = toCenter.x + (to.SpanX * 0.5f);
            float toMinZ = toCenter.y - (to.SpanZ * 0.5f);
            float toMaxZ = toCenter.y + (to.SpanZ * 0.5f);

            float dx = toCenter.x - fromCenter.x;
            float dz = toCenter.y - fromCenter.y;

            Vector3 gatePosition;
            Vector3 gateScale;
            Vector3 supportScale;

            if (Mathf.Abs(dx) >= Mathf.Abs(dz))
            {
                float overlapMinZ = Mathf.Max(fromMinZ, toMinZ);
                float overlapMaxZ = Mathf.Min(fromMaxZ, toMaxZ);
                float overlapLen = overlapMaxZ - overlapMinZ;
                if (overlapLen <= RoadWidth * 0.75f)
                {
                    return false;
                }

                float fromEdge = dx >= 0f
                    ? fromCenter.x + (from.SpanX * 0.5f - RoadWidth * 0.5f)
                    : fromCenter.x - (from.SpanX * 0.5f - RoadWidth * 0.5f);
                float toEdge = dx >= 0f
                    ? toCenter.x - (to.SpanX * 0.5f - RoadWidth * 0.5f)
                    : toCenter.x + (to.SpanX * 0.5f - RoadWidth * 0.5f);

                float seamGap = Mathf.Abs(toEdge - fromEdge);
                float centerX = (fromEdge + toEdge) * 0.5f;
                float centerZ = (overlapMinZ + overlapMaxZ) * 0.5f;
                float barrierLength = Mathf.Max(RoadWidth, overlapLen - 1.0f);

                gatePosition = new Vector3(centerX, GateBaseY, centerZ);
                gateScale = new Vector3(GateThickness, GateHeight, barrierLength);

                float supportWidth = Mathf.Max(RoadWidth, seamGap + (RoadWidth * 0.95f));
                supportScale = new Vector3(supportWidth, GateSupportHeight, barrierLength + 2.0f);
            }
            else
            {
                float overlapMinX = Mathf.Max(fromMinX, toMinX);
                float overlapMaxX = Mathf.Min(fromMaxX, toMaxX);
                float overlapLen = overlapMaxX - overlapMinX;
                if (overlapLen <= RoadWidth * 0.75f)
                {
                    return false;
                }

                float fromEdge = dz >= 0f
                    ? fromCenter.y + (from.SpanZ * 0.5f - RoadWidth * 0.5f)
                    : fromCenter.y - (from.SpanZ * 0.5f - RoadWidth * 0.5f);
                float toEdge = dz >= 0f
                    ? toCenter.y - (to.SpanZ * 0.5f - RoadWidth * 0.5f)
                    : toCenter.y + (to.SpanZ * 0.5f - RoadWidth * 0.5f);

                float seamGap = Mathf.Abs(toEdge - fromEdge);
                float centerX = (overlapMinX + overlapMaxX) * 0.5f;
                float centerZ = (fromEdge + toEdge) * 0.5f;
                float barrierLength = Mathf.Max(RoadWidth, overlapLen - 1.0f);

                gatePosition = new Vector3(centerX, GateBaseY, centerZ);
                gateScale = new Vector3(barrierLength, GateHeight, GateThickness);

                float supportDepth = Mathf.Max(RoadWidth, seamGap + (RoadWidth * 0.95f));
                supportScale = new Vector3(barrierLength + 2.0f, GateSupportHeight, supportDepth);
            }

            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Gate_" + link.GateRegionId + "_" + link.ConnectorName;
            gate.transform.SetParent(gatesRoot, false);
            gate.transform.position = gatePosition;
            gate.transform.rotation = Quaternion.identity;
            gate.transform.localScale = gateScale;

            Renderer renderer = gate.GetComponent<Renderer>();
            if (renderer != null && barrierMat != null)
            {
                renderer.sharedMaterial = barrierMat;
            }

            Collider gateCollider = gate.GetComponent<Collider>();
            if (gateCollider != null)
            {
                gateCollider.isTrigger = false;
            }

            RegionGateBarrier gateBarrier = gate.GetComponent<RegionGateBarrier>();
            if (gateBarrier == null)
            {
                gateBarrier = gate.AddComponent<RegionGateBarrier>();
            }

            Renderer[] renderers = renderer != null ? new[] { renderer } : Array.Empty<Renderer>();
            gateBarrier.ConfigureForEditor(link.GateRegionId, gateCollider, renderers);
            gateBarrier.ApplyLockedState(true);
            SetGeneratedStatic(gate);

            GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cube);
            support.name = "GateSupport";
            support.transform.SetParent(gate.transform, false);
            support.transform.localPosition = new Vector3(0f, GateSupportY - GateBaseY, 0f);
            support.transform.localRotation = Quaternion.identity;
            support.transform.localScale = supportScale;

            Renderer supportRenderer = support.GetComponent<Renderer>();
            if (supportRenderer != null)
            {
                Object.DestroyImmediate(supportRenderer);
            }

            SetGeneratedStatic(support);

            return true;
        }

        private static bool TryFindRegionInfo(List<RegionRuntimeInfo> regions, string regionId, out RegionRuntimeInfo info)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Def.RegionId == regionId)
                {
                    info = regions[i];
                    return true;
                }
            }

            info = default;
            return false;
        }

        private static void AssignAnchors(List<BuildingEntry> entries, Transform poiRoot)
        {
            Transform firstRestaurant = null;
            Transform firstDestination = null;
            Transform firstGas = null;
            int rs = 1;
            int ds = 1;
            var gasAssignedByRegion = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                BuildingEntry e = entries[i];
                if (e.Go == null)
                {
                    continue;
                }

                OrderBuildingAnchor a = e.Go.GetComponent<OrderBuildingAnchor>();
                if (a == null)
                {
                    a = e.Go.AddComponent<OrderBuildingAnchor>();
                }

                a.RegionId = e.RegionId;

                bool assignGasForRegion = !gasAssignedByRegion.Contains(e.RegionId) && e.RegionOrder == 0;
                if (assignGasForRegion)
                {
                    a.Role = OrderBuildingRole.GasStation;
                    a.AnchorId = "G_" + e.RegionId;
                    a.DisplayName = "Gas Station (" + e.RegionId + ")";
                    e.Go.name = "GasStationBuilding_" + e.RegionId;
                    gasAssignedByRegion.Add(e.RegionId);
                    if (firstGas == null)
                    {
                        firstGas = e.Go.transform;
                    }
                    continue;
                }

                if ((e.RegionOrder % 5) == 0)
                {
                    a.Role = OrderBuildingRole.Restaurant;
                    a.AnchorId = "R" + rs;
                    a.DisplayName = "Restaurant " + rs + " (" + e.RegionId + ")";
                    if (firstRestaurant == null)
                    {
                        firstRestaurant = e.Go.transform;
                        e.Go.name = "RestaurantBuilding";
                    }
                    rs++;
                }
                else
                {
                    a.Role = OrderBuildingRole.Destination;
                    a.AnchorId = "D" + ds;
                    a.DisplayName = "Destination " + ds + " (" + e.RegionId + ")";
                    if (firstDestination == null)
                    {
                        firstDestination = e.Go.transform;
                        e.Go.name = "DestinationBuilding";
                    }
                    ds++;
                }
            }

            if (firstGas == null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    BuildingEntry e = entries[i];
                    if (e == null || e.Go == null)
                    {
                        continue;
                    }

                    OrderBuildingAnchor anchor = e.Go.GetComponent<OrderBuildingAnchor>();
                    if (anchor != null && anchor.Role == OrderBuildingRole.GasStation)
                    {
                        firstGas = e.Go.transform;
                        break;
                    }
                }
            }

            if (firstGas == null)
            {
                firstGas = firstRestaurant;
            }

            CreatePoiRef(poiRoot, "RestaurantBuildingRef", firstRestaurant);
            CreatePoiRef(poiRoot, "DestinationBuildingRef", firstDestination);
            CreatePoiRef(poiRoot, "GasStationBuildingRef", firstGas);
        }

        private static GameObject InstantiateBuilding(List<string> assets, int index, Transform parent)
        {
            if (assets == null || assets.Count == 0) return null;
            string path = assets[index % assets.Count];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go == null) go = Object.Instantiate(prefab);
            if (go == null) return null;
            go.transform.SetParent(parent, true);
            go.name = "Building_" + index.ToString("000") + "_" + Path.GetFileNameWithoutExtension(path);
            return go;
        }

        private static GameObject InstantiateBuildingForTheme(
            List<string> allAssets,
            List<string> skylineAssets,
            List<string> lowriseAssets,
            RegionVisualTheme theme,
            int globalIndex,
            int regionOrder,
            Transform parent)
        {
            List<string> chosenPool = allAssets;
            float seed = (globalIndex + 1) * 0.331f + theme.SeedOffset + (regionOrder * 0.017f);
            float skylineChance = theme.PreferSkyline ? 0.62f : 0.14f;
            bool useSkyline = Deterministic01(seed) < skylineChance;

            if (useSkyline && skylineAssets != null && skylineAssets.Count > 0)
            {
                chosenPool = skylineAssets;
            }
            else if (!useSkyline && lowriseAssets != null && lowriseAssets.Count > 0)
            {
                chosenPool = lowriseAssets;
            }
            else if (allAssets != null && allAssets.Count > 0)
            {
                chosenPool = allAssets;
            }

            return InstantiateBuilding(chosenPool, globalIndex + Mathf.Abs(regionOrder * 7), parent);
        }

        private static List<string> CollectBuildingAssets()
        {
            var list = new List<string>(64);
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { ExternalsRoot });
            for (int i = 0; guids != null && i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.StartsWith(ExternalsRoot, StringComparison.OrdinalIgnoreCase)) continue;
                string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!ContainsKeyword(n, BuildingNameIncludeKeywords)) continue;
                if (ContainsKeyword(n, BuildingNameExcludeKeywords)) continue;
                list.Add(path);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        private static Dictionary<string, List<string>> BuildRegionBuildingPools(List<string> allAssets)
        {
            var pools = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (allAssets == null || allAssets.Count == 0)
            {
                return pools;
            }

            for (int i = 0; i < RegionThemes.Length; i++)
            {
                string regionId = RegionThemes[i].RegionId;
                string[] keywords = GetThemeAssetKeywords(regionId);
                List<string> filtered = FilterAssetsByPathKeyword(allAssets, keywords);
                if (filtered.Count == 0)
                {
                    filtered = allAssets;
                }

                pools[regionId] = filtered;
            }

            return pools;
        }

        private static List<string> GetRegionBuildingPool(
            Dictionary<string, List<string>> pools,
            string regionId,
            List<string> fallback)
        {
            if (pools != null)
            {
                List<string> list;
                if (pools.TryGetValue(regionId, out list) && list != null && list.Count > 0)
                {
                    return list;
                }
            }

            return fallback;
        }

        private static string[] GetThemeAssetKeywords(string regionId)
        {
            if (string.Equals(regionId, "central", StringComparison.Ordinal))
            {
                return CentralThemeKeywords;
            }

            if (string.Equals(regionId, "rushdistrict", StringComparison.Ordinal))
            {
                return RushDistrictThemeKeywords;
            }

            if (string.Equals(regionId, "frostlands", StringComparison.Ordinal))
            {
                return FrostlandsThemeKeywords;
            }

            if (string.Equals(regionId, "hillcrest", StringComparison.Ordinal))
            {
                return HillcrestThemeKeywords;
            }

            if (string.Equals(regionId, "stormcoast", StringComparison.Ordinal))
            {
                return StormcoastThemeKeywords;
            }

            if (string.Equals(regionId, "oldtown", StringComparison.Ordinal))
            {
                return OldTownThemeKeywords;
            }

            if (string.Equals(regionId, "seaside", StringComparison.Ordinal))
            {
                return SeasideThemeKeywords;
            }

            return CentralThemeKeywords;
        }

        private static List<string> FilterAssetsByPathKeyword(List<string> source, string[] keywords)
        {
            var filtered = new List<string>(source != null ? source.Count : 0);
            if (source == null || source.Count == 0 || keywords == null || keywords.Length == 0)
            {
                return filtered;
            }

            for (int i = 0; i < source.Count; i++)
            {
                string path = source[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string lowered = path.ToLowerInvariant();
                if (ContainsKeyword(lowered, keywords))
                {
                    filtered.Add(path);
                }
            }

            return filtered;
        }

        private static List<string> FilterAssetsByKeyword(List<string> source, string keyword, bool include)
        {
            var filtered = new List<string>(source != null ? source.Count : 0);
            if (source == null || string.IsNullOrEmpty(keyword))
            {
                return filtered;
            }

            for (int i = 0; i < source.Count; i++)
            {
                string path = source[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool hasKeyword = name.Contains(keyword);
                if ((include && hasKeyword) || (!include && !hasKeyword))
                {
                    filtered.Add(path);
                }
            }

            return filtered;
        }

        private static bool ContainsKeyword(string value, string[] keywords)
        {
            if (string.IsNullOrEmpty(value) || keywords == null)
            {
                return false;
            }

            for (int i = 0; i < keywords.Length; i++)
            {
                if (string.IsNullOrEmpty(keywords[i]))
                {
                    continue;
                }

                if (value.Contains(keywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static RegionVisualTheme GetTheme(string regionId)
        {
            for (int i = 0; i < RegionThemes.Length; i++)
            {
                if (string.Equals(RegionThemes[i].RegionId, regionId, StringComparison.Ordinal))
                {
                    return RegionThemes[i];
                }
            }

            return RegionThemes[0];
        }

        private static Material EnsureRegionSurfaceMaterial(
            string regionId,
            string materialType,
            Color color,
            float metallic,
            float smoothness)
        {
            string folder = RegionMaterialRoot + "/" + regionId;
            EnsureFolder(folder);
            string path = folder + "/RunCity_" + materialType + "_" + regionId + ".mat";
            return EnsureMaterial(path, color, metallic, smoothness);
        }

        private static void ApplyBuildingTheme(
            GameObject building,
            string regionId,
            RegionVisualTheme theme,
            Dictionary<string, Material> materialCache)
        {
            if (building == null)
            {
                return;
            }

            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] shared = renderer.sharedMaterials;
                bool changed = false;
                for (int j = 0; j < shared.Length; j++)
                {
                    Material src = shared[j];
                    if (src == null)
                    {
                        continue;
                    }

                    Material themed = GetOrCreateThemedBuildingMaterial(src, regionId, theme, materialCache);
                    if (themed != null && themed != src)
                    {
                        shared[j] = themed;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = shared;
                }
            }
        }

        private static Material GetOrCreateThemedBuildingMaterial(
            Material source,
            string regionId,
            RegionVisualTheme theme,
            Dictionary<string, Material> materialCache)
        {
            if (source == null)
            {
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceKey = string.IsNullOrEmpty(sourcePath) ? source.name : sourcePath;
            string cacheKey = regionId + "|" + sourceKey;
            Material cached;
            if (materialCache != null && materialCache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                return cached;
            }

            string folder = RegionMaterialRoot + "/" + regionId + "/Buildings";
            EnsureFolder(folder);
            string safeName = SanitizeName(Path.GetFileNameWithoutExtension(sourceKey));
            string themedPath = folder + "/Bld_" + safeName + "_" + StableHashHex(sourceKey) + ".mat";

            Material themed = AssetDatabase.LoadAssetAtPath<Material>(themedPath);
            if (themed == null)
            {
                themed = new Material(source);
                AssetDatabase.CreateAsset(themed, themedPath);
            }
            else
            {
                themed.shader = source.shader;
                themed.CopyPropertiesFromMaterial(source);
            }

            ApplyBuildingTint(themed, theme);
            themed.enableInstancing = true;
            EditorUtility.SetDirty(themed);

            if (materialCache != null)
            {
                materialCache[cacheKey] = themed;
            }

            return themed;
        }

        private static void ApplyBuildingTint(Material material, RegionVisualTheme theme)
        {
            if (material == null)
            {
                return;
            }

            bool hasBase = material.HasProperty("_BaseColor");
            bool hasColor = material.HasProperty("_Color");
            Color baseColor = Color.white;

            if (hasBase)
            {
                baseColor = material.GetColor("_BaseColor");
            }
            else if (hasColor)
            {
                baseColor = material.GetColor("_Color");
            }

            Color tintedTarget = MultiplyColor(baseColor, theme.BuildingTint);
            Color tinted = Color.Lerp(baseColor, tintedTarget, Mathf.Clamp01(theme.BuildingTintStrength));
            tinted.a = baseColor.a;

            if (hasBase)
            {
                material.SetColor("_BaseColor", tinted);
            }

            if (hasColor)
            {
                material.SetColor("_Color", tinted);
            }
        }

        private static Color MultiplyColor(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
        }

        private static string SanitizeName(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return "Material";
            }

            char[] chars = source.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') ||
                          (c >= 'A' && c <= 'Z') ||
                          (c >= '0' && c <= '9') ||
                          c == '_';
                if (!ok)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static string StableHashHex(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619;
                    }
                }

                return hash.ToString("X8");
            }
        }

        private static float Deterministic01(float seed)
        {
            float value = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        private static float DeterministicRange(float seed, float min, float max)
        {
            return Mathf.Lerp(min, max, Deterministic01(seed));
        }

        private static int RemoveLegacyRoots(Scene scene, GameObject cityRoot)
        {
            int removed = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == cityRoot)
                {
                    continue;
                }

                if (!IsLegacyRootName(root.name))
                {
                    continue;
                }

                Object.DestroyImmediate(root);
                removed++;
            }

            return removed;
        }

        private static int RemoveDuplicateCityRoots(Scene scene, GameObject keep)
        {
            if (keep == null)
            {
                return 0;
            }

            int removed = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == keep)
                {
                    continue;
                }

                if (!string.Equals(root.name, "CityRoot", StringComparison.Ordinal))
                {
                    continue;
                }

                Object.DestroyImmediate(root);
                removed++;
            }

            return removed;
        }

        private static bool IsLegacyRootName(string rootName)
        {
            if (string.IsNullOrEmpty(rootName))
            {
                return false;
            }

            return string.Equals(rootName, "WorldRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "RoadRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "BlockRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "BuildingRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "RoadsRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "BlocksRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "BuildingsRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "POIRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "OrderPointsRoot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "RegionGates", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "SectorThemes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(rootName, "Ground", StringComparison.OrdinalIgnoreCase);
        }

        private static int RemoveTransientSceneObjects(Scene scene)
        {
            int removed = 0;
            OrderInteractPoint[] interactPoints = Object.FindObjectsByType<OrderInteractPoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < interactPoints.Length; i++)
            {
                OrderInteractPoint point = interactPoints[i];
                if (point == null || point.gameObject.scene != scene)
                {
                    continue;
                }

                Object.DestroyImmediate(point.gameObject);
                removed++;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                string lower = root.name.ToLowerInvariant();
                if (!lower.StartsWith("orderinteractpoint_") &&
                    !lower.StartsWith("fuelinteractpoint_") &&
                    !lower.StartsWith("runtime_interact_"))
                {
                    continue;
                }

                Object.DestroyImmediate(root);
                removed++;
            }

            return removed;
        }

        private static void CreateSectorMarker(Transform root, RegionLayoutDefinition def)
        {
            GameObject go = new GameObject("Sector_" + def.RegionId);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(def.Center.x, 0f, def.Center.y);
            go.SetActive(true);
            SectorThemeMarker marker = go.AddComponent<SectorThemeMarker>();
            marker.RegionId = def.RegionId;
        }

        private static void CreatePoiRef(Transform root, string name, Transform target)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            if (target != null) go.transform.position = target.position;
        }

        private static void ConfigureCurbSolidCollider(GameObject curb)
        {
            if (curb == null)
            {
                return;
            }

            BoxCollider box = curb.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = curb.AddComponent<BoxCollider>();
            }

            box.isTrigger = false;
            box.size = Vector3.one;
            box.center = Vector3.zero;
        }

        private static void SetGeneratedStatic(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            GameObjectUtility.SetStaticEditorFlags(go, GeneratedStaticFlags);
        }

        private static void RemoveAllColliders(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        private static void RemoveAllRigidbodies(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Rigidbody[] rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] != null)
                {
                    Object.DestroyImmediate(rigidbodies[i]);
                }
            }
        }

        private static void OptimizeBuildingRenderers(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private static Material EnsureMaterial(string path, Color color, float metallic, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureTransparentMaterial(string path, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
            }

            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
            }

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }

            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 0f);
            }

            if (mat.HasProperty("_AlphaClip"))
            {
                mat.SetFloat("_AlphaClip", 0f);
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static string ResolveScenePath()
        {
            if (File.Exists(RunScenePath)) return RunScenePath;
            string[] runGuids = AssetDatabase.FindAssets("t:Scene RunScene");
            if (runGuids != null && runGuids.Length > 0) return AssetDatabase.GUIDToAssetPath(runGuids[0]);
            return string.Empty;
        }

        private static void ResetRegionMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(RegionMaterialRoot))
            {
                return;
            }

            FileUtil.DeleteFileOrDirectory(RegionMaterialRoot);
            FileUtil.DeleteFileOrDirectory(RegionMaterialRoot + ".meta");
            AssetDatabase.Refresh();
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            GameObject root = GameObject.Find(name);
            if (root == null) root = new GameObject(name);
            root.transform.SetParent(null);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static GameObject GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent.Replace("\\", "/"));
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                AssetDatabase.CreateFolder(parent.Replace("\\", "/"), name);
            }
        }
    }

}
