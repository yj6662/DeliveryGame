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
                PickupPosition);

            EnsurePoint(
                root.transform,
                "DeliveryPoint",
                OrderPointType.Delivery,
                "D1",
                "DELIVER: Apartment",
                DeliveryPosition);

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
            Vector3 position)
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
            Transform markerTransform = pointObject.transform.Find("Marker");
            if (markerTransform != null)
            {
                Object.DestroyImmediate(markerTransform.gameObject);
            }
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
