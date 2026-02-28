using System.IO;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Delivery.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunBikeSetup
    {
        private const string PrefabFolder = "Assets/Prefabs/Run";
        private const string PlayerBikePrefabPath = PrefabFolder + "/PlayerBike.prefab";
        private const string RunScenePath = "Assets/Scenes/Run/RunScene.unity";
        private const string CarModelFolder = "Assets/Externals/kenney_car-kit/Models/FBX format";

        [MenuItem("Tools/DeliveryRun/Setup Bike Player (RunScene)")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/Scripts/Editor");

            string selectedModelPath;
            GameObject bikePrefab = CreateOrUpdateBikePrefab(out selectedModelPath);
            SetupRunScene(bikePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DeliveryRunBikeSetup] Bike setup complete. Selected car model: " + selectedModelPath);
        }

        private static GameObject CreateOrUpdateBikePrefab(out string selectedModelPath)
        {
            selectedModelPath = "(none)";

            GameObject bikeRoot = new GameObject("PlayerBike");

            Rigidbody body = bikeRoot.AddComponent<Rigidbody>();
            body.mass = 220f;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider bodyCollider = bikeRoot.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, 0.6f, 0f);
            bodyCollider.size = new Vector3(1.4f, 1.2f, 2.8f);

            Transform frontRoot = new GameObject("FrontWheelRoot").transform;
            frontRoot.SetParent(bikeRoot.transform, false);
            frontRoot.localPosition = new Vector3(0f, 0.45f, 1.0f);

            Transform rearRoot = new GameObject("RearWheelRoot").transform;
            rearRoot.SetParent(bikeRoot.transform, false);
            rearRoot.localPosition = new Vector3(0f, 0.45f, -1.0f);

            Transform visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(bikeRoot.transform, false);
            visualRoot.localPosition = Vector3.zero;

            if (!AttachCarSkin(visualRoot, out selectedModelPath))
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = "FallbackCarVisual";
                Object.DestroyImmediate(fallback.GetComponent<Collider>());
                fallback.transform.SetParent(visualRoot, false);
                fallback.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                fallback.transform.localScale = new Vector3(1.0f, 0.7f, 2.2f);
            }

            MotorbikeController bikeController = bikeRoot.AddComponent<MotorbikeController>();
            SerializedObject serializedController = new SerializedObject(bikeController);
            serializedController.FindProperty("rb").objectReferenceValue = body;
            serializedController.FindProperty("frontWheelRoot").objectReferenceValue = frontRoot;
            serializedController.FindProperty("rearWheelRoot").objectReferenceValue = rearRoot;
            serializedController.FindProperty("frontWheelVisual").objectReferenceValue = null;
            serializedController.FindProperty("rearWheelVisual").objectReferenceValue = null;
            serializedController.FindProperty("visualMesh").objectReferenceValue = visualRoot;
            serializedController.FindProperty("centerOfMassOffset").vector3Value = new Vector3(0f, -0.45f, 0f);
            serializedController.FindProperty("groundMask").intValue = ~0;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bikeRoot, PlayerBikePrefabPath);
            Object.DestroyImmediate(bikeRoot);
            return prefab;
        }

        private static bool AttachCarSkin(Transform visualRoot, out string selectedModelPath)
        {
            selectedModelPath = "(none)";
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { CarModelFolder });
            if (guids == null || guids.Length == 0)
            {
                return false;
            }

            string bestPath = string.Empty;
            int bestScore = int.MinValue;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string lowered = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                int score = ScoreCarModel(lowered);
                if (score <= int.MinValue + 1)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = path;
                }
            }

            if (string.IsNullOrEmpty(bestPath))
            {
                return false;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(bestPath);
            if (asset == null)
            {
                return false;
            }

            GameObject skin = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (skin == null)
            {
                skin = Object.Instantiate(asset);
            }

            if (skin == null)
            {
                return false;
            }

            skin.name = "CarSkin";
            skin.transform.SetParent(visualRoot, false);
            skin.transform.localPosition = Vector3.zero;
            skin.transform.localRotation = Quaternion.identity;
            skin.transform.localScale = Vector3.one;

            NormalizeScale(skin.transform, 2.6f);
            RemovePhysicsComponents(skin.transform);
            AlignModelBaseToY(skin.transform, 0.02f);

            selectedModelPath = bestPath;
            return true;
        }

        private static int ScoreCarModel(string loweredName)
        {
            if (string.IsNullOrEmpty(loweredName))
            {
                return int.MinValue;
            }

            if (ContainsAny(loweredName, "debris", "wheel", "cone", "box", "tractor", "kart"))
            {
                return int.MinValue;
            }

            int score = 0;
            if (loweredName.Contains("delivery")) score += 100;
            if (loweredName.Contains("van")) score += 70;
            if (loweredName.Contains("sedan")) score += 65;
            if (loweredName.Contains("suv")) score += 60;
            if (loweredName.Contains("hatchback")) score += 55;
            if (loweredName.Contains("taxi")) score += 50;
            if (loweredName.Contains("race")) score += 35;
            if (loweredName.Contains("truck")) score += 20;
            return score;
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

            float delta = targetY - bounds.min.y;
            root.position += new Vector3(0f, delta, 0f);
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

        private static void SetupRunScene(GameObject bikePrefab)
        {
            if (!File.Exists(RunScenePath))
            {
                Debug.LogError("[DeliveryRunBikeSetup] RunScene not found: " + RunScenePath);
                return;
            }

            Scene runScene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            MotorbikeController bike = Object.FindAnyObjectByType<MotorbikeController>();
            if (bike == null && bikePrefab != null)
            {
                GameObject bikeInstance = PrefabUtility.InstantiatePrefab(bikePrefab, runScene) as GameObject;
                if (bikeInstance != null)
                {
                    bikeInstance.name = "PlayerBike";
                    bikeInstance.transform.position = new Vector3(0f, 1f, 0f);
                    bike = bikeInstance.GetComponent<MotorbikeController>();
                }
            }

            EnsureGroundFallback();
            EnsureFollowCamera(bike != null ? bike.transform : null);

            EditorSceneManager.MarkSceneDirty(runScene);
            EditorSceneManager.SaveScene(runScene);
            Debug.Log("[DeliveryRunBikeSetup] RunScene configured: " + RunScenePath);
        }

        private static void EnsureGroundFallback()
        {
            if (Object.FindAnyObjectByType<RoadSurface>() != null)
            {
                return;
            }

            Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (collider.name == "Ground")
                {
                    return;
                }
            }

            GameObject ground = new GameObject("Ground");
            ground.transform.position = Vector3.zero;
            BoxCollider box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(200f, 1f, 200f);
            box.center = new Vector3(0f, -0.5f, 0f);
        }

        private static void EnsureFollowCamera(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindAnyObjectByType<Camera>();
            }

            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            SimpleFollowCamera follow = camera.GetComponent<SimpleFollowCamera>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<SimpleFollowCamera>();
            }

            SerializedObject followSo = new SerializedObject(follow);
            followSo.FindProperty("preventClipping").boolValue = true;
            followSo.FindProperty("heightOffset").floatValue = 13f;
            followSo.FindProperty("obstacleMask").intValue = 1 << 0;
            followSo.FindProperty("occlusionPivotHeight").floatValue = 1.4f;
            followSo.FindProperty("collisionRadius").floatValue = 0.48f;
            followSo.FindProperty("collisionBuffer").floatValue = 0.25f;
            followSo.FindProperty("minDistanceFromTarget").floatValue = 2.2f;
            followSo.FindProperty("collisionBackoffStep").floatValue = 0.4f;
            followSo.FindProperty("collisionResolveSteps").intValue = 10;
            followSo.FindProperty("nearClipWhenOccluded").floatValue = 0.01f;
            followSo.FindProperty("defaultNearClip").floatValue = 0.03f;
            followSo.FindProperty("fadeOccludingRenderers").boolValue = true;
            followSo.FindProperty("occluderFadeAlpha").floatValue = 0.22f;
            followSo.FindProperty("maxFadedOccluders").intValue = 12;
            followSo.FindProperty("useRendererBoundsOcclusion").boolValue = true;
            followSo.FindProperty("occluderBoundsPadding").floatValue = 0.2f;
            followSo.FindProperty("minOccluderHeight").floatValue = 1.25f;
            followSo.FindProperty("rendererCacheRefreshSeconds").floatValue = 1.0f;
            followSo.FindProperty("tuneCameraClipPlanes").boolValue = true;
            followSo.FindProperty("tunedFarClip").floatValue = 1800f;
            followSo.FindProperty("followDistance").floatValue = 8f;
            followSo.FindProperty("smoothTime").floatValue = 0.3f;
            followSo.FindProperty("yawFollowStrength").floatValue = 0.45f;
            followSo.FindProperty("yawSmoothTime").floatValue = 0.32f;
            followSo.FindProperty("yawDeadZoneDegrees").floatValue = 1.1f;
            followSo.FindProperty("extraBackAtHighSpeed").floatValue = 4f;
            followSo.FindProperty("followSharpnessLowSpeed").floatValue = 8f;
            followSo.FindProperty("followSharpnessHighSpeed").floatValue = 2.8f;
            followSo.FindProperty("enableOrthographicBackstepGuard").boolValue = true;
            followSo.FindProperty("safeZ").floatValue = 0.05f;
            followSo.FindProperty("safeZMargin").floatValue = 0.02f;
            followSo.FindProperty("scanRadiusMultiplier").floatValue = 1.5f;
            followSo.FindProperty("checkInterval").floatValue = 0.14f;
            followSo.FindProperty("backstepSmoothTime").floatValue = 0.32f;
            followSo.FindProperty("backstepHysteresis").floatValue = 0.18f;
            followSo.FindProperty("backstepExpandSharpness").floatValue = 6f;
            followSo.FindProperty("backstepReleaseSharpness").floatValue = 2f;
            followSo.FindProperty("maxExtraDistance").floatValue = 200f;
            followSo.FindProperty("drawDebugGizmos").boolValue = false;
            followSo.ApplyModifiedPropertiesWithoutUndo();

            if (target != null)
            {
                follow.SetTarget(target);
                camera.transform.position = target.position + new Vector3(-10f, 10f, -10f);
                camera.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            }

            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 1800f;

            if (camera.GetComponent<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }
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
