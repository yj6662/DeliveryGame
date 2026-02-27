using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using DeliveryRun.Delivery.Vehicle;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunRunSceneContentGenerator
    {
        private sealed class Candidates
        {
            public readonly List<string> Road = new List<string>(64);
            public readonly List<string> Block = new List<string>(64);
            public readonly List<string> Building = new List<string>(128);
            public readonly List<string> KenneyBike = new List<string>(32);
            public readonly List<string> All = new List<string>(256);
        }

        private const string RunScenePath = "Assets/Scenes/Run/RunScene.unity";
        private const string PlayerBikePrefabPath = "Assets/Prefabs/Run/PlayerBike.prefab";
        private const int BlocksX = 5;
        private const int BlocksZ = 5;
        private const float BlockSize = 40f;
        private const float RoadWidth = 10f;
        private const float BlockMargin = 6f;
        private const int BuildingMin = 4;
        private const int BuildingMax = 10;
        private const int Seed = 12345;

        [MenuItem("Tools/DeliveryRun/Generate RunScene Content (Externals)")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Scripts/Editor");
            EnsureFolder("Assets/Scripts/Delivery/World");
            EnsureFolder("Assets/Scripts/Delivery/Vehicle");
            EnsureFolder("Assets/Prefabs/Run");
            EnsureFolder("Assets/Prefabs/Run/World");

            Type brainType;
            Type vcamType;
            bool cmReady = EnsureCinemachine(out brainType, out vcamType);

            string scenePath = ResolveRunScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new InvalidOperationException("[RunSceneContentGen] RunScene path not found.");
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject worldRoot = GetOrCreate("WorldRoot");
            ClearChildren(worldRoot.transform);

            Candidates c = ScanExternals();
            Debug.Log(
                "[RunSceneContentGen] Candidates Road=" + c.Road.Count +
                ", Block=" + c.Block.Count +
                ", Building=" + c.Building.Count +
                ", KenneyBike=" + c.KenneyBike.Count);

            int roadCount;
            int blockCount;
            int buildingCount;
            BuildWorld(worldRoot.transform, c, out roadCount, out blockCount, out buildingCount);

            string selectedSkinPath;
            GameObject bikePrefab = EnsurePlayerBikePrefab(c, out selectedSkinPath);
            Transform bike = EnsureBikeInScene(scene, bikePrefab);

            bool brainCreated;
            bool vcamCreated;
            bool cmUsed = SetupCamera(bike, cmReady, brainType, vcamType, out brainCreated, out vcamCreated);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RunSceneContentGen] WorldRoot created: " + (worldRoot != null));
            Debug.Log("[RunSceneContentGen] Generated Roads=" + roadCount + ", Blocks=" + blockCount + ", Buildings=" + buildingCount);
            Debug.Log("[RunSceneContentGen] Selected Kenney skin: " + (string.IsNullOrEmpty(selectedSkinPath) ? "(none)" : selectedSkinPath));
            Debug.Log("[RunSceneContentGen] Cinemachine used: " + cmUsed + ", BrainCreated: " + brainCreated + ", VCamCreated: " + vcamCreated);
        }

        private static bool EnsureCinemachine(out Type brainType, out Type vcamType)
        {
            FindCinemachineTypes(out brainType, out vcamType);
            if (brainType != null && vcamType != null)
            {
                return true;
            }

            bool installOk = TryInstallPackage("com.unity.cinemachine", 120);
            if (installOk)
            {
                AssetDatabase.Refresh();
            }

            FindCinemachineTypes(out brainType, out vcamType);
            bool ready = brainType != null && vcamType != null;
            if (!ready)
            {
                Debug.LogWarning("[RunSceneContentGen] Cinemachine unavailable, fallback camera will be used.");
            }

            return ready;
        }

        private static void FindCinemachineTypes(out Type brainType, out Type vcamType)
        {
            brainType = FindType("Cinemachine.CinemachineBrain", "Unity.Cinemachine.CinemachineBrain");
            vcamType = FindType(
                "Cinemachine.CinemachineVirtualCamera",
                "Unity.Cinemachine.CinemachineCamera",
                "Cinemachine.CinemachineCamera");
        }

        private static Type FindType(params string[] names)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < names.Length; i++)
            {
                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type t = assemblies[a].GetType(names[i], false);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static bool TryInstallPackage(string packageName, int timeoutSeconds)
        {
            AddRequest req;
            try
            {
                req = Client.Add(packageName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RunSceneContentGen] Package add failed to start: " + ex.Message);
                return false;
            }

            DateTime started = DateTime.UtcNow;
            while (!req.IsCompleted && (DateTime.UtcNow - started).TotalSeconds < timeoutSeconds)
            {
                Thread.Sleep(200);
            }

            if (!req.IsCompleted)
            {
                Debug.LogWarning("[RunSceneContentGen] Package add timed out: " + packageName);
                return false;
            }

            if (req.Status == StatusCode.Failure)
            {
                Debug.LogWarning("[RunSceneContentGen] Package add failed: " + (req.Error != null ? req.Error.message : "unknown"));
                return false;
            }

            return true;
        }

        private static string ResolveRunScenePath()
        {
            if (File.Exists(RunScenePath))
            {
                return RunScenePath;
            }

            string[] g = AssetDatabase.FindAssets("t:Scene RunScene");
            if (g != null && g.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(g[0]);
            }

            g = AssetDatabase.FindAssets("t:Scene Run");
            if (g != null && g.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(g[0]);
            }

            return string.Empty;
        }

        private static Candidates ScanExternals()
        {
            var c = new Candidates();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Externals" });
            for (int i = 0; guids != null && i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                c.All.Add(path);
                string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (ContainsAny(n, "road", "street", "asphalt", "lane", "intersection", "cross", "corner"))
                {
                    c.Road.Add(path);
                }

                if (ContainsAny(n, "block", "tile", "ground", "floor", "plate"))
                {
                    c.Block.Add(path);
                }

                if (ContainsAny(n, "building", "house", "shop", "tower", "skyscraper", "apartment"))
                {
                    c.Building.Add(path);
                }

                if (n.Contains("kenney") && ContainsAny(n, "bike", "motor", "scooter", "motorcycle", "vehicle"))
                {
                    c.KenneyBike.Add(path);
                }
            }

            if (c.Building.Count == 0 && c.All.Count > 0)
            {
                for (int i = 0; i < c.All.Count; i++)
                {
                    string n = Path.GetFileNameWithoutExtension(c.All[i]).ToLowerInvariant();
                    if (!ContainsAny(n, "bike", "car", "player", "character", "wheel"))
                    {
                        c.Building.Add(c.All[i]);
                    }
                }
            }

            if (c.Building.Count == 0)
            {
                c.Building.AddRange(c.All);
            }

            return c;
        }

        private static bool ContainsAny(string src, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (src.Contains(keys[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void BuildWorld(Transform worldRoot, Candidates c, out int roads, out int blocks, out int buildings)
        {
            Transform roadRoot = new GameObject("RoadRoot").transform;
            roadRoot.SetParent(worldRoot, false);
            Transform blockRoot = new GameObject("BlockRoot").transform;
            blockRoot.SetParent(worldRoot, false);
            Transform buildingRoot = new GameObject("BuildingRoot").transform;
            buildingRoot.SetParent(worldRoot, false);

            roads = 0;
            blocks = 0;
            buildings = 0;

            float spacing = BlockSize + RoadWidth;
            float totalX = (BlocksX * BlockSize) + ((BlocksX + 1) * RoadWidth);
            float totalZ = (BlocksZ * BlockSize) + ((BlocksZ + 1) * RoadWidth);

            for (int x = 0; x <= BlocksX; x++)
            {
                float px = (-totalX * 0.5f) + (RoadWidth * 0.5f) + (x * spacing);
                CreateRoadStrip(roadRoot, new Vector3(px, -0.05f, 0f), new Vector3(RoadWidth, 0.1f, totalZ));
                roads++;
            }

            for (int z = 0; z <= BlocksZ; z++)
            {
                float pz = (-totalZ * 0.5f) + (RoadWidth * 0.5f) + (z * spacing);
                CreateRoadStrip(roadRoot, new Vector3(0f, -0.05f, pz), new Vector3(totalX, 0.1f, RoadWidth));
                roads++;
            }

            System.Random rng = new System.Random(Seed);
            float startX = -((BlocksX - 1) * spacing * 0.5f);
            float startZ = -((BlocksZ - 1) * spacing * 0.5f);
            float inBlock = (BlockSize * 0.5f) - BlockMargin;

            for (int bx = 0; bx < BlocksX; bx++)
            {
                for (int bz = 0; bz < BlocksZ; bz++)
                {
                    Vector3 center = new Vector3(startX + (bx * spacing), 0f, startZ + (bz * spacing));
                    CreateBlock(c, blockRoot, center, rng);
                    blocks++;

                    int count = rng.Next(BuildingMin, BuildingMax + 1);
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 pos = center + new Vector3(Rand(rng, -inBlock, inBlock), 0f, Rand(rng, -inBlock, inBlock));
                        float y = 90f * rng.Next(0, 4);
                        CreateBuilding(c, buildingRoot, pos, y, rng);
                        buildings++;
                    }
                }
            }
        }

        private static void CreateRoadStrip(Transform parent, Vector3 pos, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "RoadStrip";
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    Material m = new Material(shader);
                    if (m.HasProperty("_Color"))
                    {
                        m.color = new Color(0.08f, 0.08f, 0.09f, 1f);
                    }

                    r.sharedMaterial = m;
                }
            }
        }

        private static void CreateBlock(Candidates c, Transform parent, Vector3 center, System.Random rng)
        {
            if (c.Block.Count > 0)
            {
                string path = c.Block[rng.Next(0, c.Block.Count)];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    GameObject g = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (g != null)
                    {
                        g.name = "Block_" + prefab.name;
                        g.transform.SetParent(parent, true);
                        g.transform.position = center;
                        EnsureCollider(g);
                        RemoveRigidbodies(g);
                        return;
                    }
                }
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "Block_Fallback";
            fallback.transform.SetParent(parent, false);
            fallback.transform.position = center + new Vector3(0f, -0.1f, 0f);
            fallback.transform.localScale = new Vector3(BlockSize, 0.2f, BlockSize);
        }

        private static void CreateBuilding(Candidates c, Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            if (c.Building.Count > 0)
            {
                string path = c.Building[rng.Next(0, c.Building.Count)];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    GameObject g = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (g != null)
                    {
                        g.name = "Building_" + prefab.name;
                        g.transform.SetParent(parent, true);
                        g.transform.position = pos;
                        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                        RemoveRigidbodies(g);
                        EnsureCollider(g);
                        return;
                    }
                }
            }

            float w = Rand(rng, 4f, 10f);
            float h = Rand(rng, 8f, 24f);
            float d = Rand(rng, 4f, 10f);
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "Building_Fallback";
            fallback.transform.SetParent(parent, false);
            fallback.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
            fallback.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            fallback.transform.localScale = new Vector3(w, h, d);
        }

        private static GameObject EnsurePlayerBikePrefab(Candidates c, out string selectedSkinPath)
        {
            selectedSkinPath = string.Empty;
            if (!File.Exists(PlayerBikePrefabPath))
            {
                CreateMinimalBikePrefab();
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerBikePrefabPath);
            try
            {
                EnsureBikeRig(root);

                Transform visualRoot = root.transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    visualRoot = new GameObject("VisualRoot").transform;
                    visualRoot.SetParent(root.transform, false);
                }

                ClearChildren(visualRoot);
                selectedSkinPath = PickKenney(c.KenneyBike);
                if (!string.IsNullOrEmpty(selectedSkinPath))
                {
                    GameObject skin = AssetDatabase.LoadAssetAtPath<GameObject>(selectedSkinPath);
                    if (skin != null)
                    {
                        GameObject inst = PrefabUtility.InstantiatePrefab(skin, root.scene) as GameObject;
                        if (inst == null)
                        {
                            inst = UnityEngine.Object.Instantiate(skin);
                        }

                        if (inst != null)
                        {
                            inst.transform.SetParent(visualRoot, false);
                            NormalizeScale(inst.transform, 2f);
                            RemovePhysicsComponents(inst.transform);
                        }
                    }
                }
                else
                {
                    GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fallback.name = "FallbackBikeVisual";
                    UnityEngine.Object.DestroyImmediate(fallback.GetComponent<Collider>());
                    fallback.transform.SetParent(visualRoot, false);
                    fallback.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                    fallback.transform.localScale = new Vector3(0.8f, 0.6f, 2.0f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerBikePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerBikePrefabPath);
        }

        private static string PickKenney(List<string> list)
        {
            if (list == null || list.Count == 0)
            {
                return string.Empty;
            }

            string best = list[0];
            int bestScore = Score(best);
            for (int i = 1; i < list.Count; i++)
            {
                int s = Score(list[i]);
                if (s > bestScore)
                {
                    best = list[i];
                    bestScore = s;
                }
            }

            return best;
        }

        private static int Score(string path)
        {
            string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            int s = 0;
            if (n.Contains("kenney")) s += 8;
            if (n.Contains("motorcycle")) s += 6;
            if (n.Contains("motorbike")) s += 5;
            if (n.Contains("bike")) s += 4;
            if (n.Contains("scooter")) s += 3;
            if (n.Contains("vehicle")) s += 2;
            return s;
        }

        private static void CreateMinimalBikePrefab()
        {
            GameObject bike = new GameObject("PlayerBike");
            EnsureBikeRig(bike);
            PrefabUtility.SaveAsPrefabAsset(bike, PlayerBikePrefabPath);
            Object.DestroyImmediate(bike);
        }

        private static void EnsureBikeRig(GameObject bike)
        {
            Rigidbody rb = bike.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = bike.AddComponent<Rigidbody>();
                rb.mass = 220f;
                rb.linearDamping = 0.02f;
                rb.angularDamping = 0.8f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (bike.GetComponent<Collider>() == null)
            {
                CapsuleCollider c = bike.AddComponent<CapsuleCollider>();
                c.center = new Vector3(0f, 0.6f, 0f);
                c.radius = 0.35f;
                c.height = 1.6f;
                c.direction = 1;
            }

            Transform frontRoot = bike.transform.Find("FrontWheelRoot");
            if (frontRoot == null)
            {
                frontRoot = new GameObject("FrontWheelRoot").transform;
                frontRoot.SetParent(bike.transform, false);
            }
            frontRoot.localPosition = new Vector3(0f, 0.6f, 0.9f);

            Transform rearRoot = bike.transform.Find("RearWheelRoot");
            if (rearRoot == null)
            {
                rearRoot = new GameObject("RearWheelRoot").transform;
                rearRoot.SetParent(bike.transform, false);
            }
            rearRoot.localPosition = new Vector3(0f, 0.6f, -0.9f);

            Transform frontVisual = frontRoot.Find("FrontWheelVisual");
            if (frontVisual == null)
            {
                frontVisual = CreateWheelVisual(frontRoot, "FrontWheelVisual");
            }

            Transform rearVisual = rearRoot.Find("RearWheelVisual");
            if (rearVisual == null)
            {
                rearVisual = CreateWheelVisual(rearRoot, "RearWheelVisual");
            }

            MotorbikeController controller = bike.GetComponent<MotorbikeController>();
            if (controller == null)
            {
                controller = bike.AddComponent<MotorbikeController>();
            }

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("rb").objectReferenceValue = rb;
            so.FindProperty("frontWheelRoot").objectReferenceValue = frontRoot;
            so.FindProperty("rearWheelRoot").objectReferenceValue = rearRoot;
            so.FindProperty("frontWheelVisual").objectReferenceValue = frontVisual;
            so.FindProperty("rearWheelVisual").objectReferenceValue = rearVisual;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreateWheelVisual(Transform parent, string name)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.34f, 0.08f, 0.34f);
            return wheel.transform;
        }

        private static Transform EnsureBikeInScene(Scene scene, GameObject prefab)
        {
            MotorbikeController existing = Object.FindAnyObjectByType<MotorbikeController>();
            if (existing != null)
            {
                return existing.transform;
            }

            if (prefab == null)
            {
                throw new InvalidOperationException("[RunSceneContentGen] PlayerBike prefab missing.");
            }

            GameObject g = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (g == null)
            {
                g = Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(g, scene);
            }

            g.name = "PlayerBike";
            g.transform.position = new Vector3(0f, 1.2f, 0f);
            g.transform.rotation = Quaternion.identity;
            return g.transform;
        }

        private static bool SetupCamera(Transform target, bool cmReady, Type brainType, Type vcamType, out bool brainCreated, out bool vcamCreated)
        {
            brainCreated = false;
            vcamCreated = false;

            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = Object.FindAnyObjectByType<Camera>();
            }

            if (cam == null)
            {
                GameObject c = new GameObject("Main Camera");
                c.tag = "MainCamera";
                cam = c.AddComponent<Camera>();
            }

            cam.nearClipPlane = 0.001f;
            cam.farClipPlane = 5000f;

            GameObject proxy = GetOrCreate("CameraFollowProxy");
            SpeedFollowProxyDriver driver = proxy.GetComponent<SpeedFollowProxyDriver>();
            if (driver == null)
            {
                driver = proxy.AddComponent<SpeedFollowProxyDriver>();
            }
            driver.SetTarget(target);

            if (cmReady && brainType != null && vcamType != null)
            {
                Component brain = cam.GetComponent(brainType);
                if (brain == null)
                {
                    brain = cam.gameObject.AddComponent(brainType);
                    brainCreated = true;
                }

                SimpleFollowCamera sf = cam.GetComponent<SimpleFollowCamera>();
                if (sf != null)
                {
                    ConfigureSimpleFollowDefaults(sf);
                    sf.enabled = false;
                }

                GameObject vcamObj = GetOrCreate("RunVCam");
                Component vcam = vcamObj.GetComponent(vcamType);
                if (vcam == null)
                {
                    vcam = vcamObj.AddComponent(vcamType);
                    vcamCreated = true;
                }

                SetObj(vcam, proxy.transform, "Follow", "m_Follow", "TrackingTarget");
                SetObj(vcam, target, "LookAt", "m_LookAt", "LookAtTarget");
                SetInt(vcam, 10, "Priority", "m_Priority");
                TrySetVcamLens(vcam, 13f, 0.001f);
                return true;
            }

            SimpleFollowCamera fallback = cam.GetComponent<SimpleFollowCamera>();
            if (fallback == null)
            {
                fallback = cam.gameObject.AddComponent<SimpleFollowCamera>();
            }
            ConfigureSimpleFollowDefaults(fallback);
            fallback.enabled = true;
            fallback.SetTarget(target);
            return false;
        }

        private static void ConfigureSimpleFollowDefaults(SimpleFollowCamera follow)
        {
            if (follow == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(follow);
            so.FindProperty("preventClipping").boolValue = true;
            so.FindProperty("heightOffset").floatValue = 13f;
            so.FindProperty("obstacleMask").intValue = ~0;
            so.FindProperty("occlusionPivotHeight").floatValue = 1.4f;
            so.FindProperty("collisionRadius").floatValue = 0.48f;
            so.FindProperty("collisionBuffer").floatValue = 0.25f;
            so.FindProperty("minDistanceFromTarget").floatValue = 2.2f;
            so.FindProperty("collisionBackoffStep").floatValue = 0.4f;
            so.FindProperty("collisionResolveSteps").intValue = 10;
            so.FindProperty("nearClipWhenOccluded").floatValue = 0.001f;
            so.FindProperty("defaultNearClip").floatValue = 0.001f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObj(Component c, UnityEngine.Object obj, params string[] names)
        {
            Type t = c.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo p = t.GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType.IsAssignableFrom(obj.GetType()))
                {
                    p.SetValue(c, obj, null);
                    return;
                }

                FieldInfo f = t.GetField(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType.IsAssignableFrom(obj.GetType()))
                {
                    f.SetValue(c, obj);
                    return;
                }
            }
        }

        private static void SetInt(Component c, int value, params string[] names)
        {
            Type t = c.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo p = t.GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(int))
                {
                    p.SetValue(c, value, null);
                    return;
                }

                FieldInfo f = t.GetField(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(int))
                {
                    f.SetValue(c, value);
                    return;
                }
            }
        }

        private static void TrySetVcamLens(Component vcam, float orthoSize, float nearClipPlane)
        {
            if (vcam == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(vcam);
            bool serializedChanged = false;
            SerializedProperty pOrtho = so.FindProperty("Lens.Orthographic");
            if (pOrtho != null)
            {
                pOrtho.boolValue = true;
                serializedChanged = true;
            }
            else
            {
                pOrtho = so.FindProperty("m_Lens.Orthographic");
                if (pOrtho != null)
                {
                    pOrtho.boolValue = true;
                    serializedChanged = true;
                }
            }

            SerializedProperty pSize = so.FindProperty("Lens.OrthographicSize");
            if (pSize != null)
            {
                pSize.floatValue = orthoSize;
                serializedChanged = true;
            }
            else
            {
                pSize = so.FindProperty("m_Lens.OrthographicSize");
                if (pSize != null)
                {
                    pSize.floatValue = orthoSize;
                    serializedChanged = true;
                }
            }

            SerializedProperty pNear = so.FindProperty("Lens.NearClipPlane");
            if (pNear != null)
            {
                pNear.floatValue = Mathf.Clamp(nearClipPlane, 0.001f, 0.5f);
                serializedChanged = true;
            }
            else
            {
                pNear = so.FindProperty("m_Lens.NearClipPlane");
                if (pNear != null)
                {
                    pNear.floatValue = Mathf.Clamp(nearClipPlane, 0.001f, 0.5f);
                    serializedChanged = true;
                }
            }

            if (serializedChanged)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            Type type = vcam.GetType();
            FieldInfo lensField = type.GetField("m_Lens", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (lensField != null)
            {
                object lens = lensField.GetValue(vcam);
                if (TrySetLensStruct(ref lens, orthoSize, nearClipPlane))
                {
                    lensField.SetValue(vcam, lens);
                    return;
                }
            }

            PropertyInfo lensProp = type.GetProperty("Lens", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (lensProp != null && lensProp.CanRead && lensProp.CanWrite)
            {
                object lens = lensProp.GetValue(vcam, null);
                if (TrySetLensStruct(ref lens, orthoSize, nearClipPlane))
                {
                    lensProp.SetValue(vcam, lens, null);
                }
            }
        }

        private static bool TrySetLensStruct(ref object lensStruct, float orthoSize, float nearClipPlane)
        {
            if (lensStruct == null)
            {
                return false;
            }

            Type lensType = lensStruct.GetType();
            bool changed = false;

            FieldInfo orthoField = lensType.GetField("Orthographic", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (orthoField != null && orthoField.FieldType == typeof(bool))
            {
                orthoField.SetValue(lensStruct, true);
                changed = true;
            }
            else
            {
                PropertyInfo orthoProp = lensType.GetProperty("Orthographic", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (orthoProp != null && orthoProp.CanWrite && orthoProp.PropertyType == typeof(bool))
                {
                    orthoProp.SetValue(lensStruct, true, null);
                    changed = true;
                }
            }

            FieldInfo sizeField = lensType.GetField("OrthographicSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (sizeField != null && sizeField.FieldType == typeof(float))
            {
                sizeField.SetValue(lensStruct, orthoSize);
                changed = true;
            }
            else
            {
                PropertyInfo sizeProp = lensType.GetProperty("OrthographicSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (sizeProp != null && sizeProp.CanWrite && sizeProp.PropertyType == typeof(float))
                {
                    sizeProp.SetValue(lensStruct, orthoSize, null);
                    changed = true;
                }
            }

            float clampedNear = Mathf.Clamp(nearClipPlane, 0.001f, 0.5f);
            FieldInfo nearField = lensType.GetField("NearClipPlane", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (nearField != null && nearField.FieldType == typeof(float))
            {
                nearField.SetValue(lensStruct, clampedNear);
                changed = true;
            }
            else
            {
                PropertyInfo nearProp = lensType.GetProperty("NearClipPlane", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nearProp != null && nearProp.CanWrite && nearProp.PropertyType == typeof(float))
                {
                    nearProp.SetValue(lensStruct, clampedNear, null);
                    changed = true;
                }
            }

            return changed;
        }

        private static void RemoveRigidbodies(GameObject root)
        {
            Rigidbody[] arr = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < arr.Length; i++)
            {
                Object.DestroyImmediate(arr[i]);
            }
        }

        private static void RemovePhysicsComponents(Transform root)
        {
            Rigidbody[] rbs = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++) Object.DestroyImmediate(rbs[i]);
            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) Object.DestroyImmediate(cols[i]);
        }

        private static void EnsureCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>(true) != null)
            {
                return;
            }

            go.AddComponent<BoxCollider>();
        }

        private static void NormalizeScale(Transform root, float targetLen)
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs == null || rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float len = Mathf.Max(b.size.x, b.size.z);
            if (len <= 0.001f) return;
            float s = Mathf.Clamp(targetLen / len, 0.05f, 20f);
            root.localScale *= s;
        }

        private static float Rand(System.Random r, float min, float max)
        {
            return min + ((float)r.NextDouble() * (max - min));
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

        private static GameObject GetOrCreate(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            return go;
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(t.GetChild(i).gameObject);
            }
        }
    }
}
