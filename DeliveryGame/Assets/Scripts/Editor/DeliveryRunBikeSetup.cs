using System.IO;
using DeliveryRun.Delivery.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunBikeSetup
    {
        private const string PrefabFolder = "Assets/Prefabs/Run";
        private const string PlayerBikePrefabPath = PrefabFolder + "/PlayerBike.prefab";
        private const string RunScenePath = "Assets/Scenes/Run/RunScene.unity";

        [MenuItem("Tools/DeliveryRun/Setup Bike Player (RunScene)")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/Scripts/Editor");

            GameObject bikePrefab = CreateOrUpdateBikePrefab();
            SetupRunScene(bikePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DeliveryRunBikeSetup] Bike setup complete.");
        }

        private static GameObject CreateOrUpdateBikePrefab()
        {
            GameObject bikeRoot = new GameObject("PlayerBike");

            Rigidbody body = bikeRoot.AddComponent<Rigidbody>();
            body.mass = 220f;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            CapsuleCollider bodyCollider = bikeRoot.AddComponent<CapsuleCollider>();
            bodyCollider.center = new Vector3(0f, 0.6f, 0f);
            bodyCollider.radius = 0.35f;
            bodyCollider.height = 1.6f;
            bodyCollider.direction = 1;

            Transform frontRoot = new GameObject("FrontWheelRoot").transform;
            frontRoot.SetParent(bikeRoot.transform, false);
            frontRoot.localPosition = new Vector3(0f, 0.6f, 0.9f);

            Transform rearRoot = new GameObject("RearWheelRoot").transform;
            rearRoot.SetParent(bikeRoot.transform, false);
            rearRoot.localPosition = new Vector3(0f, 0.6f, -0.9f);

            Transform frontVisual = CreateWheelVisual("FrontWheelVisual", frontRoot);
            Transform rearVisual = CreateWheelVisual("RearWheelVisual", rearRoot);

            MotorbikeController bikeController = bikeRoot.AddComponent<MotorbikeController>();
            SerializedObject serializedController = new SerializedObject(bikeController);
            serializedController.FindProperty("rb").objectReferenceValue = body;
            serializedController.FindProperty("frontWheelRoot").objectReferenceValue = frontRoot;
            serializedController.FindProperty("rearWheelRoot").objectReferenceValue = rearRoot;
            serializedController.FindProperty("frontWheelVisual").objectReferenceValue = frontVisual;
            serializedController.FindProperty("rearWheelVisual").objectReferenceValue = rearVisual;
            serializedController.FindProperty("centerOfMassOffset").vector3Value = new Vector3(0f, -0.45f, 0f);
            serializedController.FindProperty("groundMask").intValue = ~0;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bikeRoot, PlayerBikePrefabPath);
            Object.DestroyImmediate(bikeRoot);
            return prefab;
        }

        private static Transform CreateWheelVisual(string name, Transform parent)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.34f, 0.08f, 0.34f);
            return wheel.transform;
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

            EnsureGround();
            EnsureFollowCamera(bike != null ? bike.transform : null);

            EditorSceneManager.MarkSceneDirty(runScene);
            EditorSceneManager.SaveScene(runScene);
            Debug.Log("[DeliveryRunBikeSetup] RunScene configured: " + RunScenePath);
        }

        private static void EnsureGround()
        {
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

            if (target != null)
            {
                follow.SetTarget(target);
                camera.transform.position = target.position + new Vector3(-10f, 10f, -10f);
                camera.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            }

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
