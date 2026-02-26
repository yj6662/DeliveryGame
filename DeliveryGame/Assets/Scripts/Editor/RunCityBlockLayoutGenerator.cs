using System;
using System.Collections.Generic;
using System.IO;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
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
        private const float BlockSize = 40f;
        private const float BlockHeight = 2f;
        private const float BlockCenterY = 1f;
        private const float RoadWidth = 14f;
        private const float RoadHeight = 0.2f;
        private const float LotInset = 11f;
        private const int LotCount = 16;

        public static void Generate()
        {
            string runScenePath = ResolveRunScenePath();
            if (string.IsNullOrEmpty(runScenePath))
            {
                throw new InvalidOperationException("[RunCityBlockLayoutGenerator] RunScene path not found.");
            }

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

            List<GameObject> buildingPool = CollectBuildingCandidates(runScene, cityRoot.transform, buildingsRoot.transform);

            ClearChildren(roadsRoot.transform);
            ClearChildren(blocksRoot.transform);
            ClearChildren(poiRoot.transform);

            int roadCount = BuildRoads(roadsRoot.transform);
            int blockCount = BuildBlocks(blocksRoot.transform);

            List<Vector3> lotPositions = BuildLotPositions();
            int movedCount;
            int placeholderCount;
            List<GameObject> placedBuildings = PlaceBuildings(buildingPool, buildingsRoot.transform, lotPositions, out movedCount, out placeholderCount);

            int removedLegacyRootCount = RemoveLegacyRoots(runScene, cityRoot);

            string restaurantName;
            string destinationName;
            AssignOrderBuildingAnchors(placedBuildings, poiRoot.transform, out restaurantName, out destinationName);

            EditorSceneManager.MarkSceneDirty(runScene);
            EditorSceneManager.SaveScene(runScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RunCityBlockLayoutGenerator] Scene: " + runScenePath);
            Debug.Log("[RunCityBlockLayoutGenerator] Roads created: " + roadCount + ", Blocks created: " + blockCount);
            Debug.Log("[RunCityBlockLayoutGenerator] Buildings moved: " + movedCount + ", placeholders created: " + placeholderCount + ", total lots: " + LotCount);
            Debug.Log("[RunCityBlockLayoutGenerator] Anchors: Restaurant=" + restaurantName + ", Destination=" + destinationName);
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

        private static int BuildRoads(Transform roadsRoot)
        {
            float halfSpan = (BlockSize * 2f + RoadWidth * 3f) * 0.5f;
            float edgeCenter = halfSpan - (RoadWidth * 0.5f);
            float[] lineCenters = { -edgeCenter, 0f, edgeCenter };
            int created = 0;

            for (int i = 0; i < lineCenters.Length; i++)
            {
                CreateRoadStrip(
                    roadsRoot,
                    "Road_V_" + i.ToString("00"),
                    new Vector3(lineCenters[i], -RoadHeight * 0.5f, 0f),
                    new Vector3(RoadWidth, RoadHeight, halfSpan * 2f));
                created++;
            }

            for (int i = 0; i < lineCenters.Length; i++)
            {
                CreateRoadStrip(
                    roadsRoot,
                    "Road_H_" + i.ToString("00"),
                    new Vector3(0f, -RoadHeight * 0.5f, lineCenters[i]),
                    new Vector3(halfSpan * 2f, RoadHeight, RoadWidth));
                created++;
            }

            return created;
        }

        private static void CreateRoadStrip(Transform parent, string name, Vector3 worldPosition, Vector3 scale)
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
        }

        private static int BuildBlocks(Transform blocksRoot)
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
                created++;
            }

            return created;
        }

        private static List<Vector3> BuildLotPositions()
        {
            Vector2[] centers = GetBlockCenters();
            float[] offsets = { -LotInset, LotInset };
            float blockTopY = BlockCenterY + (BlockHeight * 0.5f);
            var lots = new List<Vector3>(LotCount);

            for (int i = 0; i < centers.Length; i++)
            {
                for (int z = 0; z < offsets.Length; z++)
                {
                    for (int x = 0; x < offsets.Length; x++)
                    {
                        lots.Add(new Vector3(
                            centers[i].x + offsets[x],
                            blockTopY,
                            centers[i].y + offsets[z]));
                    }
                }
            }

            return lots;
        }

        private static Vector2[] GetBlockCenters()
        {
            float offset = (BlockSize + RoadWidth) * 0.5f;
            return new[]
            {
                new Vector2(-offset, -offset),
                new Vector2(offset, -offset),
                new Vector2(-offset, offset),
                new Vector2(offset, offset)
            };
        }

        private static List<GameObject> CollectBuildingCandidates(Scene scene, Transform cityRoot, Transform buildingsRoot)
        {
            var result = new List<GameObject>(64);
            var seen = new HashSet<int>();

            for (int i = 0; i < buildingsRoot.childCount; i++)
            {
                AddCandidate(buildingsRoot.GetChild(i).gameObject, result, seen);
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform root = roots[r].transform;
                if (root == cityRoot)
                {
                    continue;
                }

                GatherCandidatesRecursive(root, result, seen);
            }

            result.Sort(CompareByNameThenId);
            return result;
        }

        private static void GatherCandidatesRecursive(Transform current, List<GameObject> result, HashSet<int> seen)
        {
            GameObject go = current.gameObject;
            if (IsBuildingCandidate(go))
            {
                AddCandidate(go, result, seen);
            }

            for (int i = 0; i < current.childCount; i++)
            {
                GatherCandidatesRecursive(current.GetChild(i), result, seen);
            }
        }

        private static bool IsBuildingCandidate(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            if (go.GetComponentInParent<MotorbikeController>() != null)
            {
                return false;
            }

            string lowered = go.name.ToLowerInvariant();
            if (ContainsAny(lowered, "road", "block", "ground", "player", "bike", "camera", "order", "pickup", "delivery", "ui", "hud", "canvas", "marker", "poi"))
            {
                return false;
            }

            bool nameLooksBuilding = ContainsAny(lowered, "building", "house", "shop", "tower", "apartment", "skyscraper");
            bool underBuildingRoot = HasAncestorNamed(go.transform, "BuildingRoot") || HasAncestorNamed(go.transform, "BuildingsRoot");
            bool hasMeshRenderer = go.GetComponent<MeshRenderer>() != null;
            if (!nameLooksBuilding && !underBuildingRoot && !hasMeshRenderer)
            {
                return false;
            }

            if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null || go.GetComponent<Canvas>() != null)
            {
                return false;
            }

            if (go.GetComponentInParent<OrderInteractPoint>() != null)
            {
                return false;
            }

            return true;
        }

        private static bool ContainsAny(string source, params string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (source.Contains(words[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAncestorNamed(Transform t, string ancestorName)
        {
            Transform current = t.parent;
            while (current != null)
            {
                if (string.Equals(current.name, ancestorName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void AddCandidate(GameObject go, List<GameObject> result, HashSet<int> seen)
        {
            if (go == null)
            {
                return;
            }

            int id = go.GetInstanceID();
            if (seen.Contains(id))
            {
                return;
            }

            seen.Add(id);
            result.Add(go);
        }

        private static int CompareByNameThenId(GameObject left, GameObject right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int byName = string.Compare(left.name, right.name, StringComparison.Ordinal);
            if (byName != 0)
            {
                return byName;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private static List<GameObject> PlaceBuildings(
            List<GameObject> buildingPool,
            Transform buildingsRoot,
            List<Vector3> lotPositions,
            out int movedCount,
            out int placeholderCount)
        {
            movedCount = 0;
            placeholderCount = 0;

            var placed = new List<GameObject>(LotCount);
            int usableExisting = Mathf.Min(buildingPool.Count, LotCount);

            for (int i = 0; i < LotCount; i++)
            {
                GameObject building;
                if (i < usableExisting)
                {
                    building = buildingPool[i];
                    movedCount++;
                    building.SetActive(true);
                }
                else
                {
                    building = CreatePlaceholderBuilding(i);
                    placeholderCount++;
                }

                MoveBuildingToLot(building, buildingsRoot, lotPositions[i], i);
                placed.Add(building);
            }

            for (int i = LotCount; i < buildingPool.Count; i++)
            {
                GameObject extra = buildingPool[i];
                if (extra == null)
                {
                    continue;
                }

                extra.transform.SetParent(buildingsRoot, true);
                extra.SetActive(false);
            }

            return placed;
        }

        private static GameObject CreatePlaceholderBuilding(int index)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Building_" + index.ToString("00");
            return go;
        }

        private static void MoveBuildingToLot(GameObject building, Transform buildingsRoot, Vector3 lotPosition, int index)
        {
            if (building == null)
            {
                return;
            }

            building.transform.SetParent(buildingsRoot, true);

            Rigidbody[] rigidbodies = building.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Object.DestroyImmediate(rigidbodies[i]);
            }

            float y = ComputePlacementY(building, lotPosition.y);
            building.transform.position = new Vector3(lotPosition.x, y, lotPosition.z);
            building.transform.rotation = Quaternion.Euler(0f, (index % 4) * 90f, 0f);
        }

        private static float ComputePlacementY(GameObject go, float blockTopY)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return blockTopY + 1f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float minOffsetFromPivot = bounds.min.y - go.transform.position.y;
            return blockTopY - minOffsetFromPivot + 0.05f;
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
            out string destinationName)
        {
            restaurantName = "(none)";
            destinationName = "(none)";

            if (placedBuildings == null || placedBuildings.Count == 0)
            {
                return;
            }

            OrderBuildingAnchor[] existingAnchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existingAnchors.Length; i++)
            {
                Object.DestroyImmediate(existingAnchors[i]);
            }

            GameObject restaurant = placedBuildings[0];
            GameObject destination = placedBuildings[placedBuildings.Count - 1];

            restaurant.name = "RestaurantBuilding";
            destination.name = "DestinationBuilding";

            OrderBuildingAnchor restaurantAnchor = restaurant.GetComponent<OrderBuildingAnchor>();
            if (restaurantAnchor == null)
            {
                restaurantAnchor = restaurant.AddComponent<OrderBuildingAnchor>();
            }

            restaurantAnchor.Role = OrderBuildingRole.Restaurant;

            OrderBuildingAnchor destinationAnchor = destination.GetComponent<OrderBuildingAnchor>();
            if (destinationAnchor == null)
            {
                destinationAnchor = destination.AddComponent<OrderBuildingAnchor>();
            }

            destinationAnchor.Role = OrderBuildingRole.Destination;

            CreatePoiReference(poiRoot, "RestaurantBuildingRef", restaurant.transform);
            CreatePoiReference(poiRoot, "DestinationBuildingRef", destination.transform);

            restaurantName = restaurant.name;
            destinationName = destination.name;
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
    }
}
