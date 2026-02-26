using System;
using System.Collections.Generic;
using System.IO;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
    public static class RunCityBlockLayoutGenerator
    {
        private const string PreferredRunScenePath = "Assets/Scenes/Run/RunScene.unity";
        private const string CityKitModelFolder = "Assets/Externals/kenney_city-kit-commercial_2.1/Models/FBX format";
        private const string MaterialRoot = "Assets/Materials/Generated";
        private const string RoadMaterialPath = MaterialRoot + "/RunCity_Road_Asphalt.mat";
        private const string BlockMaterialPath = MaterialRoot + "/RunCity_Block_Concrete.mat";

        private const int BlockGridSize = 4; // 4x4 => 16 blocks, world area about 4x of 2x2
        private const int BuildingsPerBlock = 8; // 3x3 lots without center

        private const float BlockSize = 40f;
        private const float BlockHeight = 0.6f; // reduced step between road and block
        private const float BlockCenterY = 0.3f;
        private const float RoadWidth = 22f;
        private const float RoadHeight = 0.2f;
        private const float LotInset = 10.0f;

        public static void Generate()
        {
            string runScenePath = ResolveRunScenePath();
            if (string.IsNullOrEmpty(runScenePath))
            {
                throw new InvalidOperationException("[RunCityBlockLayoutGenerator] RunScene path not found.");
            }

            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialRoot);

            Material roadMat = EnsureGeneratedMaterial(RoadMaterialPath, new Color(0.105f, 0.105f, 0.11f, 1f), 0.03f, 0.22f);
            Material blockMat = EnsureGeneratedMaterial(BlockMaterialPath, new Color(0.38f, 0.39f, 0.42f, 1f), 0.02f, 0.08f);

            Scene runScene = EditorSceneManager.OpenScene(runScenePath, OpenSceneMode.Single);
            if (!runScene.IsValid())
            {
                throw new InvalidOperationException("[RunCityBlockLayoutGenerator] Failed to open scene: " + runScenePath);
            }

            GameObject cityRoot = GetOrCreateRoot("CityRoot");
            GameObject roadsRoot = GetOrCreateChild(cityRoot.transform, "RoadsRoot");
            GameObject blocksRoot = GetOrCreateChild(cityRoot.transform, "BlocksRoot");
            GameObject buildingsRoot = GetOrCreateChild(cityRoot.transform, "BuildingsRoot");
            GameObject poiRoot = GetOrCreateChild(cityRoot.transform, "POIRoot");

            ClearChildren(roadsRoot.transform);
            ClearChildren(blocksRoot.transform);
            ClearChildren(buildingsRoot.transform);
            ClearChildren(poiRoot.transform);

            List<string> cityBuildingAssets = CollectCityBuildingAssets();

            int roadCount = BuildRoads(roadsRoot.transform, roadMat);
            int blockCount = BuildBlocks(blocksRoot.transform, blockMat);

            int instantiatedBuildings;
            int placeholderBuildings;
            List<GameObject> placedBuildings = PlaceBuildings(
                buildingsRoot.transform,
                cityBuildingAssets,
                out instantiatedBuildings,
                out placeholderBuildings);

            int removedLegacyRootCount = RemoveLegacyRoots(runScene, cityRoot);

            string restaurantName;
            string destinationName;
            string gasStationName;
            AssignOrderBuildingAnchors(
                placedBuildings,
                poiRoot.transform,
                out restaurantName,
                out destinationName,
                out gasStationName);

            EditorSceneManager.MarkSceneDirty(runScene);
            EditorSceneManager.SaveScene(runScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RunCityBlockLayoutGenerator] Scene: " + runScenePath);
            Debug.Log("[RunCityBlockLayoutGenerator] Roads created: " + roadCount + ", Blocks created: " + blockCount);
            Debug.Log("[RunCityBlockLayoutGenerator] Buildings instantiated: " + instantiatedBuildings + ", placeholders created: " + placeholderBuildings);
            Debug.Log("[RunCityBlockLayoutGenerator] Anchors: Restaurant=" + restaurantName + ", Destination=" + destinationName + ", GasStation=" + gasStationName);
            Debug.Log("[RunCityBlockLayoutGenerator] Removed legacy roots: " + removedLegacyRootCount);
        }

        private static string ResolveRunScenePath()
        {
            if (File.Exists(PreferredRunScenePath))
            {
                return PreferredRunScenePath;
            }

            string[] guids = AssetDatabase.FindAssets("t:Scene RunScene");
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            guids = AssetDatabase.FindAssets("t:Scene Run");
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            return string.Empty;
        }

        private static Material EnsureGeneratedMaterial(string path, Color color, float metallic, float smoothness)
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

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (mat.HasProperty("_Glossiness"))
            {
                mat.SetFloat("_Glossiness", smoothness);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            GameObject root = GameObject.Find(name);
            if (root == null)
            {
                root = new GameObject(name);
            }

            root.transform.SetParent(null);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static int BuildRoads(Transform roadsRoot, Material roadMat)
        {
            float halfSpan = (BlockSize * BlockGridSize + RoadWidth * (BlockGridSize + 1)) * 0.5f;
            float[] lineCenters = BuildRoadLineCenters(halfSpan);
            int created = 0;

            for (int i = 0; i < lineCenters.Length; i++)
            {
                CreateRoadStrip(
                    roadsRoot,
                    "Road_V_" + i.ToString("00"),
                    new Vector3(lineCenters[i], -RoadHeight * 0.5f, 0f),
                    new Vector3(RoadWidth, RoadHeight, halfSpan * 2f),
                    roadMat);
                created++;
            }

            for (int i = 0; i < lineCenters.Length; i++)
            {
                CreateRoadStrip(
                    roadsRoot,
                    "Road_H_" + i.ToString("00"),
                    new Vector3(0f, -RoadHeight * 0.5f, lineCenters[i]),
                    new Vector3(halfSpan * 2f, RoadHeight, RoadWidth),
                    roadMat);
                created++;
            }

            return created;
        }

        private static float[] BuildRoadLineCenters(float halfSpan)
        {
            int roadLineCount = BlockGridSize + 1;
            var centers = new float[roadLineCount];
            float start = -halfSpan + (RoadWidth * 0.5f);
            float step = BlockSize + RoadWidth;
            for (int i = 0; i < roadLineCount; i++)
            {
                centers[i] = start + (i * step);
            }

            return centers;
        }

        private static void CreateRoadStrip(
            Transform parent,
            string name,
            Vector3 worldPosition,
            Vector3 scale,
            Material roadMat)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent, false);
            strip.transform.position = worldPosition;
            strip.transform.rotation = Quaternion.identity;
            strip.transform.localScale = scale;

            if (strip.GetComponent<RoadSurface>() == null)
            {
                strip.AddComponent<RoadSurface>();
            }

            Renderer renderer = strip.GetComponent<Renderer>();
            if (renderer != null && roadMat != null)
            {
                renderer.sharedMaterial = roadMat;
            }
        }

        private static int BuildBlocks(Transform blocksRoot, Material blockMat)
        {
            Vector2[] centers = GetBlockCenters();
            int created = 0;
            for (int i = 0; i < centers.Length; i++)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Block_" + i.ToString("00");
                block.transform.SetParent(blocksRoot, false);
                block.transform.position = new Vector3(centers[i].x, BlockCenterY, centers[i].y);
                block.transform.rotation = Quaternion.identity;
                block.transform.localScale = new Vector3(BlockSize, BlockHeight, BlockSize);

                Renderer renderer = block.GetComponent<Renderer>();
                if (renderer != null && blockMat != null)
                {
                    renderer.sharedMaterial = blockMat;
                }

                created++;
            }

            return created;
        }

        private static Vector2[] GetBlockCenters()
        {
            int total = BlockGridSize * BlockGridSize;
            var centers = new Vector2[total];
            float step = BlockSize + RoadWidth;
            float start = -((BlockGridSize - 1) * step * 0.5f);
            int idx = 0;
            for (int z = 0; z < BlockGridSize; z++)
            {
                for (int x = 0; x < BlockGridSize; x++)
                {
                    centers[idx++] = new Vector2(start + (x * step), start + (z * step));
                }
            }

            return centers;
        }

        private static List<string> CollectCityBuildingAssets()
        {
            var results = new List<string>(64);
            string[] searchFolders = { CityKitModelFolder };
            string[] guids = AssetDatabase.FindAssets("t:GameObject", searchFolders);

            for (int i = 0; guids != null && i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string filename = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!filename.Contains("building"))
                {
                    continue;
                }

                results.Add(path);
            }

            results.Sort(StringComparer.Ordinal);
            return results;
        }

        private static List<GameObject> PlaceBuildings(
            Transform buildingsRoot,
            List<string> buildingAssets,
            out int instantiatedCount,
            out int placeholderCount)
        {
            int totalLots = BlockGridSize * BlockGridSize * BuildingsPerBlock;
            var placed = new List<GameObject>(totalLots);

            instantiatedCount = 0;
            placeholderCount = 0;

            Vector2[] blockCenters = GetBlockCenters();
            float[] lotAxis = { -LotInset, 0f, LotInset };
            float blockTopY = BlockCenterY + (BlockHeight * 0.5f);
            int globalLotIndex = 0;

            for (int blockIndex = 0; blockIndex < blockCenters.Length; blockIndex++)
            {
                Vector2 blockCenter = blockCenters[blockIndex];
                for (int z = 0; z < lotAxis.Length; z++)
                {
                    for (int x = 0; x < lotAxis.Length; x++)
                    {
                        if (x == 1 && z == 1)
                        {
                            continue;
                        }

                        Vector3 lotPosition = new Vector3(
                            blockCenter.x + lotAxis[x],
                            blockTopY,
                            blockCenter.y + lotAxis[z]);

                        GameObject building = InstantiateBuildingAsset(buildingAssets, globalLotIndex, buildingsRoot);
                        if (building != null)
                        {
                            instantiatedCount++;
                        }
                        else
                        {
                            building = CreatePlaceholderBuilding(globalLotIndex);
                            building.transform.SetParent(buildingsRoot, false);
                            placeholderCount++;
                        }

                        PositionBuildingOnLot(building, lotPosition, globalLotIndex);
                        RemoveRigidbodies(building);
                        EnsureAnyCollider(building);
                        placed.Add(building);
                        globalLotIndex++;
                    }
                }
            }

            return placed;
        }

        private static GameObject InstantiateBuildingAsset(List<string> assets, int index, Transform parent)
        {
            if (assets == null || assets.Count == 0)
            {
                return null;
            }

            string path = assets[index % assets.Count];
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(asset);
            }

            if (instance == null)
            {
                return null;
            }

            instance.transform.SetParent(parent, true);
            string shortName = Path.GetFileNameWithoutExtension(path);
            instance.name = "Building_" + index.ToString("00") + "_" + shortName;
            return instance;
        }

        private static GameObject CreatePlaceholderBuilding(int index)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Building_Placeholder_" + index.ToString("00");
            go.transform.localScale = new Vector3(8f, 14f, 8f);
            return go;
        }

        private static void PositionBuildingOnLot(GameObject building, Vector3 lotPosition, int lotIndex)
        {
            if (building == null)
            {
                return;
            }

            float targetFootprint = 9.4f + ((lotIndex % 3) * 0.8f); // 9.4 ~ 11.0
            float minHeight = 10f;
            FitBuildingToLot(building, targetFootprint, minHeight);

            float y = ComputePlacementY(building, lotPosition.y);
            building.transform.position = new Vector3(lotPosition.x, y, lotPosition.z);
            building.transform.rotation = Quaternion.Euler(0f, (lotIndex % 4) * 90f, 0f);
        }

        private static void FitBuildingToLot(GameObject building, float targetFootprint, float minHeight)
        {
            if (building == null)
            {
                return;
            }

            Bounds bounds;
            if (!TryGetRendererBounds(building, out bounds))
            {
                return;
            }

            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint > 0.0001f)
            {
                float uniformScale = Mathf.Clamp(targetFootprint / footprint, 0.15f, 20f);
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
                Vector3 ls = building.transform.localScale;
                ls.y *= yScaleMul;
                building.transform.localScale = ls;
            }
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

        private static void EnsureAnyCollider(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            root.AddComponent<BoxCollider>();
        }

        private static void RemoveRigidbodies(GameObject root)
        {
            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Object.DestroyImmediate(rigidbodies[i]);
            }
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

        private static bool IsLegacyRootName(string name)
        {
            return string.Equals(name, "WorldRoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "RoadRoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "BlockRoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "BuildingRoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Ground", StringComparison.OrdinalIgnoreCase);
        }

        private static void AssignOrderBuildingAnchors(
            List<GameObject> placedBuildings,
            Transform poiRoot,
            out string restaurantName,
            out string destinationName,
            out string gasStationName)
        {
            restaurantName = "(none)";
            destinationName = "(none)";
            gasStationName = "(none)";

            if (placedBuildings == null || placedBuildings.Count == 0)
            {
                return;
            }

            OrderBuildingAnchor[] existingAnchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existingAnchors.Length; i++)
            {
                Object.DestroyImmediate(existingAnchors[i]);
            }

            int gasIndex = placedBuildings.Count / 2;
            if (gasIndex < 0) gasIndex = 0;
            if (gasIndex >= placedBuildings.Count) gasIndex = placedBuildings.Count - 1;

            Transform firstRestaurant = null;
            Transform firstDestination = null;
            Transform gasStation = null;
            int restaurantSerial = 1;
            int destinationSerial = 1;

            for (int i = 0; i < placedBuildings.Count; i++)
            {
                GameObject building = placedBuildings[i];
                if (building == null)
                {
                    continue;
                }

                OrderBuildingAnchor anchor = building.GetComponent<OrderBuildingAnchor>();
                if (anchor == null)
                {
                    anchor = building.AddComponent<OrderBuildingAnchor>();
                }

                if (i == gasIndex)
                {
                    building.name = "GasStationBuilding";
                    anchor.Role = OrderBuildingRole.GasStation;
                    anchor.AnchorId = "G1";
                    anchor.DisplayName = "Gas Station";
                    gasStation = building.transform;
                    continue;
                }

                if ((i % 4) == 0)
                {
                    anchor.Role = OrderBuildingRole.Restaurant;
                    anchor.AnchorId = "R" + restaurantSerial;
                    anchor.DisplayName = "Restaurant " + restaurantSerial;
                    if (firstRestaurant == null)
                    {
                        building.name = "RestaurantBuilding";
                        firstRestaurant = building.transform;
                    }
                    restaurantSerial++;
                }
                else
                {
                    anchor.Role = OrderBuildingRole.Destination;
                    anchor.AnchorId = "D" + destinationSerial;
                    anchor.DisplayName = "Destination " + destinationSerial;
                    if (firstDestination == null)
                    {
                        building.name = "DestinationBuilding";
                        firstDestination = building.transform;
                    }
                    destinationSerial++;
                }
            }

            if (firstRestaurant == null)
            {
                firstRestaurant = placedBuildings[0].transform;
            }

            if (firstDestination == null)
            {
                firstDestination = placedBuildings[Mathf.Max(0, placedBuildings.Count - 1)].transform;
            }

            if (gasStation == null)
            {
                gasStation = placedBuildings[gasIndex].transform;
            }

            CreatePoiReference(poiRoot, "RestaurantBuildingRef", firstRestaurant);
            CreatePoiReference(poiRoot, "DestinationBuildingRef", firstDestination);
            CreatePoiReference(poiRoot, "GasStationBuildingRef", gasStation);

            restaurantName = firstRestaurant != null ? firstRestaurant.name : "(none)";
            destinationName = firstDestination != null ? firstDestination.name : "(none)";
            gasStationName = gasStation != null ? gasStation.name : "(none)";
        }

        private static void CreatePoiReference(Transform poiRoot, string name, Transform target)
        {
            Transform marker = poiRoot.Find(name);
            if (marker == null)
            {
                marker = new GameObject(name).transform;
                marker.SetParent(poiRoot, false);
            }

            if (target == null)
            {
                marker.position = Vector3.zero;
                return;
            }

            marker.position = target.position;
            marker.rotation = Quaternion.identity;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent.Replace("\\", "/"));
            }

            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                AssetDatabase.CreateFolder(parent.Replace("\\", "/"), name);
            }
        }
    }
}
