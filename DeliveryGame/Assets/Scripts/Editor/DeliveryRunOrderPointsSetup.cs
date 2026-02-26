using DeliveryRun.Delivery.Orders;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunOrderPointsSetup
    {
        private const string PreferredRunScenePath = "Assets/Scenes/Run/RunScene.unity";
        private const string RunSceneSearchName = "RunScene";
        private const string RootName = "OrderPointsRoot";

        private static readonly Vector3 PickupPosition = new Vector3(10f, 0f, 10f);
        private static readonly Vector3 DeliveryPosition = new Vector3(120f, 0f, 80f);

        [MenuItem("Tools/DeliveryRun/Setup Order Points (RunScene)")]
        public static void GenerateAll()
        {
            string runScenePath = ResolveRunScenePath();
            if (string.IsNullOrEmpty(runScenePath))
            {
                throw new UnityException("[DeliveryRunOrderPointsSetup] Could not find RunScene asset.");
            }

            Scene scene = EditorSceneManager.OpenScene(runScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new UnityException("[DeliveryRunOrderPointsSetup] Failed to open scene: " + runScenePath);
            }

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
            }

            EnsurePoint(
                root.transform,
                "PickupPoint",
                OrderPointType.Pickup,
                "P1",
                "PICKUP: Burger Shop",
                PickupPosition,
                new Color(0.2f, 0.9f, 0.3f, 1f));

            EnsurePoint(
                root.transform,
                "DeliveryPoint",
                OrderPointType.Delivery,
                "D1",
                "DELIVER: Apartment",
                DeliveryPosition,
                new Color(0.2f, 0.45f, 1f, 1f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[DeliveryRunOrderPointsSetup] Setup completed: " + runScenePath);
        }

        private static void EnsurePoint(
            Transform root,
            string objectName,
            OrderPointType pointType,
            string pointId,
            string displayName,
            Vector3 position,
            Color markerColor)
        {
            Transform existing = root.Find(objectName);
            GameObject pointObject = existing != null ? existing.gameObject : new GameObject(objectName);
            pointObject.transform.SetParent(root, false);
            pointObject.transform.position = position;
            pointObject.transform.rotation = Quaternion.identity;

            OrderInteractPoint point = pointObject.GetComponent<OrderInteractPoint>();
            if (point == null)
            {
                point = pointObject.AddComponent<OrderInteractPoint>();
            }

            point.Configure(pointType, pointId, displayName);

            SphereCollider trigger = pointObject.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = pointObject.AddComponent<SphereCollider>();
            }

            trigger.radius = 4f;
            trigger.isTrigger = true;
            trigger.center = Vector3.zero;

            EnsureMarker(pointObject.transform, markerColor);
        }

        private static void EnsureMarker(Transform parent, Color color)
        {
            const string markerName = "Marker";

            Transform markerTransform = parent.Find(markerName);
            GameObject marker;
            if (markerTransform == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = markerName;
                marker.transform.SetParent(parent, false);
            }
            else
            {
                marker = markerTransform.gameObject;
            }

            marker.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = new Vector3(1.8f, 0.2f, 1.8f);

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Object.DestroyImmediate(markerCollider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static string ResolveRunScenePath()
        {
            if (System.IO.File.Exists(PreferredRunScenePath))
            {
                return PreferredRunScenePath;
            }

            string[] guids = AssetDatabase.FindAssets("t:Scene " + RunSceneSearchName);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
