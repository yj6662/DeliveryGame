using System;
using System.Collections.Generic;
using System.IO;
using DeliveryRun.Delivery.Traffic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunTrafficNpcPrefabGenerator
    {
        private struct VehicleCandidate
        {
            public string AssetPath;
            public string ModelName;
            public TrafficVehicleClass VehicleClass;
            public float CruiseSpeed;
            public float MaxSpeed;
            public float TargetLength;
        }

        private const string CarModelFolder = "Assets/Externals/kenney_car-kit/Models/FBX format";
        private const string PrefabRootFolder = "Assets/Prefabs/Run/Traffic";
        private const string PrefabVariantFolder = PrefabRootFolder + "/NpcVehicles";
        private const string LegacyPrefabPath = "Assets/Prefabs/Run/TrafficNpcVehicle.prefab";
        private const string CatalogPath = "Assets/Resources/Bootstrap/TrafficNpcCatalog.asset";

        public static void GenerateAll()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Run");
            EnsureFolder(PrefabRootFolder);
            EnsureFolder(PrefabVariantFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Bootstrap");

            List<VehicleCandidate> candidates = CollectCandidates();
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("[TrafficNpcPrefabGenerator] No vehicle model candidates found in " + CarModelFolder);
            }

            var generatedPrefabs = new List<GameObject>(candidates.Count);
            var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < candidates.Count; i++)
            {
                VehicleCandidate candidate = candidates[i];
                string prefabPath = PrefabVariantFolder + "/TrafficNpc_" + SanitizeForPath(candidate.ModelName) + ".prefab";
                expectedPaths.Add(prefabPath);

                GameObject prefab = CreateVariantPrefab(candidate, prefabPath);
                if (prefab != null)
                {
                    generatedPrefabs.Add(prefab);
                }
            }

            CleanupStaleVariants(expectedPaths);
            UpdateLegacyPrefab(generatedPrefabs);
            UpdateCatalog(generatedPrefabs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TrafficNpcPrefabGenerator] Generated variants: " + generatedPrefabs.Count + ", folder=" + PrefabVariantFolder);
        }

        private static List<VehicleCandidate> CollectCandidates()
        {
            var candidates = new List<VehicleCandidate>(32);
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { CarModelFolder });
            for (int i = 0; guids != null && i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                string modelName = Path.GetFileNameWithoutExtension(assetPath);
                string lowered = modelName.ToLowerInvariant();
                if (!IsVehicleModelName(lowered))
                {
                    continue;
                }

                TrafficVehicleClass vehicleClass = ClassifyVehicle(lowered);
                float cruiseSpeed;
                float maxSpeed;
                float targetLength;
                GetClassDefaults(vehicleClass, out cruiseSpeed, out maxSpeed, out targetLength);

                candidates.Add(new VehicleCandidate
                {
                    AssetPath = assetPath,
                    ModelName = modelName,
                    VehicleClass = vehicleClass,
                    CruiseSpeed = cruiseSpeed,
                    MaxSpeed = maxSpeed,
                    TargetLength = targetLength
                });
            }

            candidates.Sort((a, b) => string.CompareOrdinal(a.ModelName, b.ModelName));
            return candidates;
        }

        private static bool IsVehicleModelName(string loweredName)
        {
            if (string.IsNullOrEmpty(loweredName))
            {
                return false;
            }

            if (ContainsAny(loweredName, "wheel", "debris", "cone", "barrier", "ramp", "traffic_light", "street", "road", "building"))
            {
                return false;
            }

            return ContainsAny(
                loweredName,
                "car",
                "sedan",
                "suv",
                "hatchback",
                "van",
                "truck",
                "taxi",
                "coupe",
                "sports",
                "pickup");
        }

        private static TrafficVehicleClass ClassifyVehicle(string loweredName)
        {
            if (loweredName.Contains("taxi")) return TrafficVehicleClass.Taxi;
            if (loweredName.Contains("truck") || loweredName.Contains("pickup")) return TrafficVehicleClass.Truck;
            if (loweredName.Contains("van")) return TrafficVehicleClass.Van;
            if (loweredName.Contains("suv")) return TrafficVehicleClass.Suv;
            if (loweredName.Contains("sport") || loweredName.Contains("race") || loweredName.Contains("coupe")) return TrafficVehicleClass.Sports;
            if (loweredName.Contains("sedan")) return TrafficVehicleClass.Sedan;
            if (loweredName.Contains("hatch")) return TrafficVehicleClass.Compact;
            return TrafficVehicleClass.Service;
        }

        private static void GetClassDefaults(TrafficVehicleClass vehicleClass, out float cruiseSpeed, out float maxSpeed, out float targetLength)
        {
            cruiseSpeed = 8f;
            maxSpeed = 12f;
            targetLength = 4.0f;

            switch (vehicleClass)
            {
                case TrafficVehicleClass.Compact:
                    cruiseSpeed = 9.5f;
                    maxSpeed = 14f;
                    targetLength = 3.6f;
                    break;
                case TrafficVehicleClass.Sedan:
                    cruiseSpeed = 9f;
                    maxSpeed = 13.5f;
                    targetLength = 4.1f;
                    break;
                case TrafficVehicleClass.Suv:
                    cruiseSpeed = 8.2f;
                    maxSpeed = 12.4f;
                    targetLength = 4.5f;
                    break;
                case TrafficVehicleClass.Van:
                    cruiseSpeed = 7.2f;
                    maxSpeed = 11f;
                    targetLength = 4.8f;
                    break;
                case TrafficVehicleClass.Truck:
                    cruiseSpeed = 6.8f;
                    maxSpeed = 10.4f;
                    targetLength = 5.2f;
                    break;
                case TrafficVehicleClass.Taxi:
                    cruiseSpeed = 8.8f;
                    maxSpeed = 13f;
                    targetLength = 4.2f;
                    break;
                case TrafficVehicleClass.Sports:
                    cruiseSpeed = 10.2f;
                    maxSpeed = 15.5f;
                    targetLength = 4.0f;
                    break;
                case TrafficVehicleClass.Service:
                    cruiseSpeed = 8f;
                    maxSpeed = 12f;
                    targetLength = 4.2f;
                    break;
            }
        }

        private static GameObject CreateVariantPrefab(VehicleCandidate candidate, string prefabPath)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(candidate.AssetPath);
            if (modelAsset == null)
            {
                return null;
            }

            GameObject root = new GameObject("TrafficNpc_" + SanitizeForPath(candidate.ModelName));

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1100f;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider bodyCollider = root.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, 0.7f, 0f);
            bodyCollider.size = new Vector3(1.9f, 1.4f, 4.2f);

            TrafficNpcVehicle npc = root.AddComponent<TrafficNpcVehicle>();

            Transform visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);

            GameObject skin = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (skin == null)
            {
                skin = Object.Instantiate(modelAsset);
            }

            if (skin == null)
            {
                Object.DestroyImmediate(root);
                return null;
            }

            skin.name = "VehicleSkin";
            skin.transform.SetParent(visualRoot, false);
            skin.transform.localPosition = Vector3.zero;
            skin.transform.localRotation = Quaternion.identity;
            skin.transform.localScale = Vector3.one;

            NormalizeScale(skin.transform, candidate.TargetLength);
            RemovePhysicsComponents(skin.transform);
            AlignModelBaseToY(skin.transform, 0.05f);
            FitColliderToSkin(root.transform, bodyCollider, skin.transform);

            SerializedObject so = new SerializedObject(npc);
            so.FindProperty("vehicleId").stringValue = "traffic/npc/" + SanitizeForId(candidate.ModelName);
            so.FindProperty("displayName").stringValue = candidate.ModelName;
            so.FindProperty("vehicleClass").enumValueIndex = (int)candidate.VehicleClass;
            so.FindProperty("cruiseSpeed").floatValue = candidate.CruiseSpeed;
            so.FindProperty("maxSpeed").floatValue = candidate.MaxSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void FitColliderToSkin(Transform root, BoxCollider collider, Transform skinRoot)
        {
            if (collider == null || root == null || skinRoot == null)
            {
                return;
            }

            Bounds bounds;
            if (!TryGetRendererBounds(skinRoot.gameObject, out bounds))
            {
                return;
            }

            Vector3 center = root.InverseTransformPoint(bounds.center);
            Vector3 size = bounds.size;
            size.x = Mathf.Max(1.3f, size.x * 0.9f);
            size.y = Mathf.Max(1.1f, size.y * 0.9f);
            size.z = Mathf.Max(2.6f, size.z * 0.9f);

            collider.center = center;
            collider.size = size;
        }

        private static void NormalizeScale(Transform root, float targetLengthMeters)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float currentLength = Mathf.Max(bounds.size.x, bounds.size.z);
            if (currentLength <= 0.0001f)
            {
                return;
            }

            float scale = Mathf.Clamp(targetLengthMeters / currentLength, 0.05f, 20f);
            root.localScale *= scale;
        }

        private static void AlignModelBaseToY(Transform root, float targetY)
        {
            Bounds bounds;
            if (!TryGetRendererBounds(root.gameObject, out bounds))
            {
                return;
            }

            float delta = targetY - bounds.min.y;
            root.position += new Vector3(0f, delta, 0f);
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

        private static void RemovePhysicsComponents(Transform root)
        {
            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Object.DestroyImmediate(rigidbodies[i]);
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        private static void CleanupStaleVariants(HashSet<string> expectedPaths)
        {
            string[] existingGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabVariantFolder });
            for (int i = 0; i < existingGuids.Length; i++)
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(existingGuids[i]);
                if (expectedPaths.Contains(existingPath))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(existingPath);
            }
        }

        private static void UpdateLegacyPrefab(List<GameObject> generatedPrefabs)
        {
            if (generatedPrefabs == null || generatedPrefabs.Count == 0)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(LegacyPrefabPath);
            }

            string sourcePath = AssetDatabase.GetAssetPath(generatedPrefabs[0]);
            if (!string.IsNullOrEmpty(sourcePath))
            {
                AssetDatabase.CopyAsset(sourcePath, LegacyPrefabPath);
            }
        }

        private static void UpdateCatalog(List<GameObject> generatedPrefabs)
        {
            TrafficNpcCatalogSO catalog = AssetDatabase.LoadAssetAtPath<TrafficNpcCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TrafficNpcCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            if (generatedPrefabs == null)
            {
                catalog.VehiclePrefabs = Array.Empty<GameObject>();
            }
            else
            {
                catalog.VehiclePrefabs = generatedPrefabs.ToArray();
            }

            EditorUtility.SetDirty(catalog);
        }

        private static string SanitizeForPath(string src)
        {
            if (string.IsNullOrEmpty(src))
            {
                return "Vehicle";
            }

            char[] chars = src.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_')
                {
                    continue;
                }

                chars[i] = '_';
            }

            return new string(chars);
        }

        private static string SanitizeForId(string src)
        {
            if (string.IsNullOrEmpty(src))
            {
                return "vehicle";
            }

            string lowered = src.ToLowerInvariant();
            char[] chars = lowered.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-';
                if (!ok)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static bool ContainsAny(string source, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (source.Contains(keys[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent.Replace("\\", "/"));
            }

            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                return;
            }

            AssetDatabase.CreateFolder(parent.Replace("\\", "/"), folderName);
        }
    }
}
