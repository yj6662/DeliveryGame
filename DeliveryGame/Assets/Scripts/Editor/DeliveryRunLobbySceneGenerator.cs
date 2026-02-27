using System;
using System.Collections.Generic;
using System.IO;
using DeliveryRun.Delivery.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunLobbySceneGenerator
    {
        private const string LobbyScenePath = "Assets/Scenes/Lobby/LobbyScene.unity";
        private const string BuildingKitFolder = "Assets/Externals/kenney_building-kit/Models/FBX format";
        private const string CharacterFolder = "Assets/Externals/kenney_blocky-characters_20/Models/FBX format";
        private const string LobbyMaterialFolder = "Assets/Materials/Generated/Lobby";
        private static readonly Dictionary<int, Material> MaterialCache = new Dictionary<int, Material>(16);

        public static void GenerateAll()
        {
            MaterialCache.Clear();
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Materials/Generated");
            EnsureFolder(LobbyMaterialFolder);

            string scenePath = ResolveLobbyScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new InvalidOperationException("[LobbySceneGenerator] Lobby scene not found.");
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException("[LobbySceneGenerator] Failed to open: " + scenePath);
            }

            GameObject root = GetOrCreateRoot("LobbyWorldRoot");
            ClearChildren(root.transform);

            Transform geomRoot = CreateChild(root.transform, "Geometry").transform;
            Transform interactRoot = CreateChild(root.transform, "InteractPoints").transform;

            CreateGarageEnvironment(geomRoot);
            CreateLobbyPlayer(root.transform);
            CreateLobbyCamera(root.transform);
            EnsureDirectionalLight(root.transform);
            CreateInteractZones(interactRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LobbySceneGenerator] Lobby scene generated: " + scenePath);
        }

        private static void CreateGarageEnvironment(Transform parent)
        {
            // Height-separated slabs to avoid coplanar z-fighting.
            CreateSolid("SiteBase", parent, new Vector3(0f, -1.6f, 18f), new Vector3(120f, 3.2f, 120f), new Color(0.19f, 0.19f, 0.20f, 1f));
            CreateSolid("Driveway", parent, new Vector3(0f, -0.22f, 28f), new Vector3(94f, 0.36f, 92f), new Color(0.13f, 0.13f, 0.14f, 1f));
            CreateSolid("GarageSlab", parent, new Vector3(0f, 0.04f, 0f), new Vector3(42f, 0.16f, 32f), new Color(0.32f, 0.33f, 0.35f, 1f));
            CreateSolid("InteriorWalkway", parent, new Vector3(0f, 0.14f, -3.5f), new Vector3(28f, 0.08f, 20f), new Color(0.37f, 0.39f, 0.42f, 1f));

            // Main shell walls (semi-transparent so player is always visible).
            CreateSolid("Wall_Left", parent, new Vector3(-20f, 4.1f, 0f), new Vector3(1.4f, 8.2f, 30f), new Color(0.28f, 0.31f, 0.34f, 1f), 0.36f);
            CreateSolid("Wall_Right", parent, new Vector3(20f, 4.1f, 0f), new Vector3(1.4f, 8.2f, 30f), new Color(0.28f, 0.31f, 0.34f, 1f), 0.36f);
            CreateSolid("Wall_Back", parent, new Vector3(0f, 4.1f, -15f), new Vector3(40f, 8.2f, 1.4f), new Color(0.30f, 0.34f, 0.38f, 1f), 0.38f);
            CreateSolid("DoorFrame_Left", parent, new Vector3(-14f, 4.1f, 15f), new Vector3(11f, 8.2f, 1.4f), new Color(0.31f, 0.35f, 0.40f, 1f), 0.34f);
            CreateSolid("DoorFrame_Right", parent, new Vector3(14f, 4.1f, 15f), new Vector3(11f, 8.2f, 1.4f), new Color(0.31f, 0.35f, 0.40f, 1f), 0.34f);
            CreateSolid("DoorFrame_Top", parent, new Vector3(0f, 8.4f, 15f), new Vector3(18f, 0.9f, 1.4f), new Color(0.34f, 0.37f, 0.41f, 1f), 0.30f);

            // Side annex for better silhouette.
            CreateSolid("AnnexBody", parent, new Vector3(-30f, 2.6f, -2f), new Vector3(14f, 5.2f, 18f), new Color(0.27f, 0.30f, 0.34f, 1f), 0.34f);
            CreateSolid("AnnexDoorStep", parent, new Vector3(-23f, 0.18f, 6.5f), new Vector3(3.5f, 0.08f, 4f), new Color(0.42f, 0.43f, 0.44f, 1f));

            // Building-kit facades (semi-transparent walls, no roof pieces for fully open top).
            TryAddKitPiece("wall-doorway-wide-square.fbx", parent, new Vector3(0f, 0.02f, 15.75f), Quaternion.identity, new Vector3(9f, 8f, 1f), "Kit_FrontDoorway", true, 0.32f);
            TryAddKitPiece("wall-window-wide-square-detailed.fbx", parent, new Vector3(0f, 0.02f, -15.75f), Quaternion.identity, new Vector3(9f, 8f, 1f), "Kit_BackFacade", true, 0.32f);
            TryAddKitPiece("wall-window-wide-square.fbx", parent, new Vector3(-20.75f, 0.02f, -1f), Quaternion.Euler(0f, 90f, 0f), new Vector3(8f, 8f, 1f), "Kit_LeftFacade", true, 0.30f);
            TryAddKitPiece("wall-window-wide-square.fbx", parent, new Vector3(20.75f, 0.02f, -1f), Quaternion.Euler(0f, -90f, 0f), new Vector3(8f, 8f, 1f), "Kit_RightFacade", true, 0.30f);

            TryAddKitPiece("column-wide.fbx", parent, new Vector3(-18.8f, 0f, 13.9f), Quaternion.identity, new Vector3(2.2f, 8f, 2.2f), "Kit_Column_FL");
            TryAddKitPiece("column-wide.fbx", parent, new Vector3(18.8f, 0f, 13.9f), Quaternion.identity, new Vector3(2.2f, 8f, 2.2f), "Kit_Column_FR");
            TryAddKitPiece("column-wide.fbx", parent, new Vector3(-18.8f, 0f, -13.9f), Quaternion.identity, new Vector3(2.2f, 8f, 2.2f), "Kit_Column_BL");
            TryAddKitPiece("column-wide.fbx", parent, new Vector3(18.8f, 0f, -13.9f), Quaternion.identity, new Vector3(2.2f, 8f, 2.2f), "Kit_Column_BR");

            TryAddKitPiece("border-high.fbx", parent, new Vector3(0f, 0.02f, 58f), Quaternion.identity, new Vector3(20f, 2f, 1f), "Kit_FrontFence");
            TryAddKitPiece("border-high-corner.fbx", parent, new Vector3(-46.5f, 0.02f, 58f), Quaternion.identity, new Vector3(2f, 2f, 2f), "Kit_FenceCornerL");
            TryAddKitPiece("border-high-corner.fbx", parent, new Vector3(46.5f, 0.02f, 58f), Quaternion.Euler(0f, 90f, 0f), new Vector3(2f, 2f, 2f), "Kit_FenceCornerR");
        }

        private static void CreateLobbyPlayer(Transform parent)
        {
            GameObject existing = GameObject.Find("LobbyPlayer");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject player = new GameObject("LobbyPlayer");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(0f, 0.18f, -4.5f);
            player.transform.rotation = Quaternion.identity;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.72f;
            cc.radius = 0.34f;
            cc.center = new Vector3(0f, 0.86f, 0f);
            cc.stepOffset = 0.36f;
            cc.slopeLimit = 50f;

            player.AddComponent<LobbyAvatarController>();

            Transform visualRoot = CreateChild(player.transform, "VisualRoot").transform;
            GameObject characterPrefab = FindCharacterPrefab();
            if (characterPrefab != null)
            {
                GameObject visual = PrefabUtility.InstantiatePrefab(characterPrefab) as GameObject;
                if (visual != null)
                {
                    visual.transform.SetParent(visualRoot, false);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;
                    RemovePhysicsComponents(visual);
                }
            }
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "FallbackVisual";
                fallback.transform.SetParent(visualRoot, false);
                fallback.transform.localScale = new Vector3(0.9f, 1.65f, 0.9f);
                Object.DestroyImmediate(fallback.GetComponent<Collider>());
            }
        }

        private static void CreateLobbyCamera(Transform parent)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                GameObject any = GameObject.Find("Main Camera");
                if (any != null)
                {
                    main = any.GetComponent<Camera>();
                }
            }

            if (main == null)
            {
                GameObject cameraGo = new GameObject("Main Camera");
                main = cameraGo.AddComponent<Camera>();
                cameraGo.tag = "MainCamera";
                cameraGo.AddComponent<AudioListener>();
            }

            main.transform.SetParent(parent, false);
            main.transform.position = new Vector3(-9.5f, 10f, -9.5f);
            main.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            main.nearClipPlane = 0.01f;
            main.farClipPlane = 800f;
            main.fieldOfView = 54f;

            LobbyCameraFollow follow = main.GetComponent<LobbyCameraFollow>();
            if (follow == null)
            {
                follow = main.gameObject.AddComponent<LobbyCameraFollow>();
            }

            GameObject player = GameObject.Find("LobbyPlayer");
            if (player != null)
            {
                SerializedObject so = new SerializedObject(follow);
                SerializedProperty target = so.FindProperty("target");
                if (target != null)
                {
                    target.objectReferenceValue = player.transform;
                }

                SerializedProperty worldOffset = so.FindProperty("worldOffset");
                if (worldOffset != null)
                {
                    worldOffset.vector3Value = new Vector3(-9.5f, 10f, -9.5f);
                }

                SerializedProperty keepQuarterRotation = so.FindProperty("keepQuarterRotation");
                if (keepQuarterRotation != null)
                {
                    keepQuarterRotation.boolValue = true;
                }

                SerializedProperty quarterEuler = so.FindProperty("quarterEuler");
                if (quarterEuler != null)
                {
                    quarterEuler.vector3Value = new Vector3(35f, 45f, 0f);
                }

                SerializedProperty lookAhead = so.FindProperty("lookAhead");
                if (lookAhead != null)
                {
                    lookAhead.floatValue = 0.4f;
                }

                SerializedProperty lookHeight = so.FindProperty("lookHeight");
                if (lookHeight != null)
                {
                    lookHeight.floatValue = 1.2f;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureDirectionalLight(Transform parent)
        {
            Light existing = Object.FindFirstObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional)
            {
                existing.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                existing.intensity = 1.15f;
                return;
            }

            GameObject lightGo = new GameObject("LobbySun");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.92f, 1f);
        }

        private static void CreateInteractZones(Transform parent)
        {
            CreateZone(parent, "GarageUpgradeZone", new Vector3(-7f, 0.1f, -7f), LobbyInteractType.OpenGaragePanel, "Garage Terminal", new Color(0.2f, 0.7f, 1f, 1f));
            CreateZone(parent, "RegionUnlockZone", new Vector3(7f, 0.1f, -7f), LobbyInteractType.OpenRegionPanel, "Region Console", new Color(0.25f, 1f, 0.6f, 1f));
            CreateZone(parent, "StartDeliveryDoor", new Vector3(0f, 0.1f, 15f), LobbyInteractType.StartDelivery, "Go Delivery", new Color(1f, 0.86f, 0.3f, 1f));
        }

        private static void CreateZone(Transform parent, string name, Vector3 pos, LobbyInteractType type, string prompt, Color color)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = pos;

            LobbyInteractZone interact = zone.AddComponent<LobbyInteractZone>();
            SerializedObject so = new SerializedObject(interact);
            so.FindProperty("interactType").enumValueIndex = (int)type;
            so.FindProperty("promptText").stringValue = prompt;
            so.FindProperty("interactRadius").floatValue = 2.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(zone.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            marker.transform.localScale = new Vector3(0.9f, 0.15f, 0.9f);
            Renderer r = marker.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = CreateTransientMaterial(color);
            }
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        private static Material CreateTransientMaterial(Color color, float alpha = 1f)
        {
            Color finalColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            int key = GetColorKey(finalColor);
            Material cached;
            if (MaterialCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            string matPath = LobbyMaterialFolder + "/Lobby_" + key.ToString("X8") + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", finalColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", finalColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.02f);
            ConfigureMaterialTransparency(mat, finalColor.a);
            EditorUtility.SetDirty(mat);

            MaterialCache[key] = mat;
            return mat;
        }

        private static void CreateSolid(
            string name,
            Transform parent,
            Vector3 pos,
            Vector3 scale,
            Color color,
            float alpha = 1f,
            bool removeCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateTransientMaterial(color, alpha);
            }

            if (removeCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                {
                    Object.DestroyImmediate(col);
                }
            }
        }

        private static void TryAddKitPiece(
            string fileName,
            Transform parent,
            Vector3 pos,
            Quaternion rot,
            Vector3 scale,
            string goName,
            bool forceTransparent = false,
            float alpha = 1f)
        {
            string path = BuildingKitFolder + "/" + fileName;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return;
            }

            GameObject inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (inst == null)
            {
                return;
            }

            inst.name = goName;
            inst.transform.SetParent(parent, false);
            inst.transform.localPosition = pos;
            inst.transform.localRotation = rot;
            inst.transform.localScale = scale;
            RemovePhysicsComponents(inst);

            if (forceTransparent)
            {
                Renderer[] renderers = inst.GetComponentsInChildren<Renderer>(true);
                Color tint = new Color(0.24f, 0.27f, 0.30f, 1f);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.sharedMaterial = CreateTransientMaterial(tint, alpha);
                }
            }
        }

        private static GameObject FindCharacterPrefab()
        {
            string[] preferred =
            {
                "character-m.fbx",
                "character-k.fbx",
                "character-g.fbx",
                "character-c.fbx",
                "character-a.fbx"
            };

            for (int i = 0; i < preferred.Length; i++)
            {
                string path = CharacterFolder + "/" + preferred[i];
                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:GameObject character-", new[] { CharacterFolder });
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            Array.Sort(guids, StringComparer.Ordinal);
            string anyPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GameObject>(anyPath);
        }

        private static void RemovePhysicsComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Object.DestroyImmediate(bodies[i]);
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        private static string ResolveLobbyScenePath()
        {
            if (File.Exists(LobbyScenePath))
            {
                return LobbyScenePath;
            }

            string[] guids = AssetDatabase.FindAssets("t:Scene LobbyScene");
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

        private static GameObject CreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
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

        private static void ConfigureMaterialTransparency(Material mat, float alpha)
        {
            if (mat == null)
            {
                return;
            }

            bool transparent = alpha < 0.99f;
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", transparent ? 1f : 0f);
            }

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }

            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
            }

            if (mat.HasProperty("_AlphaClip"))
            {
                mat.SetFloat("_AlphaClip", 0f);
            }

            mat.renderQueue = transparent ? (int)UnityEngine.Rendering.RenderQueue.Transparent : -1;
        }

        private static int GetColorKey(Color color)
        {
            Color32 c = color;
            unchecked
            {
                int key = c.r;
                key = (key * 397) ^ c.g;
                key = (key * 397) ^ c.b;
                key = (key * 397) ^ c.a;
                return key;
            }
        }
    }
}
