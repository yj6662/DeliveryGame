using System;
using DeliveryRun.Delivery.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunSectorVariantsGenerator
    {
        private const string RunScenePath = "Assets/Scenes/Run/RunScene.unity";
        private const float BlockSize = 40f;
        private const float RoadWidth = 22f;
        private const int BlockGridSize = 4;

        public static void GenerateAll()
        {
            Scene runScene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            if (!runScene.IsValid())
            {
                throw new InvalidOperationException("[SectorVariantsGenerator] Failed to open scene: " + RunScenePath);
            }

            GameObject cityRoot = GameObject.Find("CityRoot");
            if (cityRoot == null)
            {
                throw new InvalidOperationException("[SectorVariantsGenerator] CityRoot not found. Run city layout generator first.");
            }

            Transform sectorRoot = EnsureChild(cityRoot.transform, "SectorThemes");
            ClearChildren(sectorRoot);

            int created = 0;
            created += CreateCentralVariant(sectorRoot);
            created += CreateRushDistrictVariant(sectorRoot);
            created += CreateFrostlandsVariant(sectorRoot);
            created += CreateHillcrestVariant(sectorRoot);
            created += CreateStormCoastVariant(sectorRoot);
            created += CreateOldTownVariant(sectorRoot);

            EditorSceneManager.MarkSceneDirty(runScene);
            EditorSceneManager.SaveScene(runScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SectorVariantsGenerator] Generated sector variants: " + created + " (central + 5 themed).");
        }

        private static int CreateCentralVariant(Transform root)
        {
            CreateSectorRoot(root, "central", true);
            return 1;
        }

        private static int CreateRushDistrictVariant(Transform root)
        {
            Transform sector = CreateSectorRoot(root, "rushdistrict", false);
            for (int i = -3; i <= 3; i++)
            {
                GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = "RushBarrier_" + (i + 3).ToString("00");
                barrier.transform.SetParent(sector, false);
                barrier.transform.localScale = new Vector3(3f, 1.2f, 1.2f);
                barrier.transform.localPosition = new Vector3(i * 28f, 0.6f, 11f);
                SetRendererColor(barrier, new Color(1f, 0.44f, 0.1f, 1f), true);
            }

            return 1;
        }

        private static int CreateFrostlandsVariant(Transform root)
        {
            Transform sector = CreateSectorRoot(root, "frostlands", false);
            Vector2[] centers = BuildBlockCenters();
            for (int i = 0; i < centers.Length; i++)
            {
                GameObject snow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                snow.name = "SnowPatch_" + i.ToString("00");
                snow.transform.SetParent(sector, false);
                snow.transform.localScale = new Vector3(BlockSize * 0.9f, 0.08f, BlockSize * 0.9f);
                snow.transform.localPosition = new Vector3(centers[i].x, 0.65f, centers[i].y);
                RemoveCollider(snow);
                SetRendererColor(snow, new Color(0.86f, 0.94f, 1f, 0.92f), true);
            }

            return 1;
        }

        private static int CreateHillcrestVariant(Transform root)
        {
            Transform sector = CreateSectorRoot(root, "hillcrest", false);
            for (int i = -2; i <= 2; i++)
            {
                float z = i * 26f;
                GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.name = "HillRamp_" + (i + 2).ToString("00");
                ramp.transform.SetParent(sector, false);
                ramp.transform.localScale = new Vector3(18f, 1.5f, 16f);
                ramp.transform.localPosition = new Vector3(0f, 0.75f, z);
                ramp.transform.localRotation = Quaternion.Euler(9f * (i % 2 == 0 ? 1f : -1f), 0f, 0f);
                SetRendererColor(ramp, new Color(0.43f, 0.39f, 0.34f, 1f), true);
            }

            return 1;
        }

        private static int CreateStormCoastVariant(Transform root)
        {
            Transform sector = CreateSectorRoot(root, "stormcoast", false);
            for (int i = -3; i <= 3; i++)
            {
                GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                puddle.name = "Puddle_" + (i + 3).ToString("00");
                puddle.transform.SetParent(sector, false);
                puddle.transform.localScale = new Vector3(9f, 0.05f, 6f);
                puddle.transform.localPosition = new Vector3(i * 22f, 0.05f, -11f);
                RemoveCollider(puddle);
                SetRendererColor(puddle, new Color(0.18f, 0.32f, 0.46f, 0.9f), true);
            }

            return 1;
        }

        private static int CreateOldTownVariant(Transform root)
        {
            Transform sector = CreateSectorRoot(root, "oldtown", false);
            for (int i = -2; i <= 2; i++)
            {
                GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arch.name = "OldTownArch_" + (i + 2).ToString("00");
                arch.transform.SetParent(sector, false);
                arch.transform.localScale = new Vector3(12f, 4.5f, 1.2f);
                arch.transform.localPosition = new Vector3(i * 34f, 2.3f, 0f);
                SetRendererColor(arch, new Color(0.6f, 0.48f, 0.35f, 1f), true);
            }

            return 1;
        }

        private static Transform CreateSectorRoot(Transform parent, string regionId, bool activeByDefault)
        {
            GameObject sector = new GameObject("Sector_" + regionId);
            sector.transform.SetParent(parent, false);
            sector.transform.localPosition = Vector3.zero;
            sector.transform.localRotation = Quaternion.identity;
            sector.transform.localScale = Vector3.one;
            sector.SetActive(activeByDefault);

            SectorThemeMarker marker = sector.AddComponent<SectorThemeMarker>();
            marker.RegionId = regionId;
            return sector.transform;
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static Vector2[] BuildBlockCenters()
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

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void SetRendererColor(GameObject go, Color color, bool opaque)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (opaque)
            {
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
                if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 0f);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            renderer.sharedMaterial = material;
        }
    }
}
