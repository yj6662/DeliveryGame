using System;
using System.IO;
using System.Reflection;
using DeliveryRun.Delivery.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunIsometricCameraSetup
    {
        private const string DefaultRunScenePath = "Assets/Scenes/Run/RunScene.unity";

        [MenuItem("Tools/DeliveryRun/Setup RunScene Isometric Camera")]
        public static void GenerateAll()
        {
            string scenePath = ResolveRunScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new InvalidOperationException("[IsoCameraSetup] RunScene path not found.");
            }

            Scene runScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            MotorbikeController bike = Object.FindFirstObjectByType<MotorbikeController>();
            if (bike == null)
            {
                throw new InvalidOperationException("[IsoCameraSetup] MotorbikeController not found in RunScene.");
            }

            GameObject proxy = GameObject.Find("CameraFollowProxy");
            if (proxy == null)
            {
                proxy = new GameObject("CameraFollowProxy");
                proxy.transform.position = bike.transform.position + new Vector3(-10f, 10f, -10f);
            }

            SpeedFollowProxyDriver driver = proxy.GetComponent<SpeedFollowProxyDriver>();
            if (driver == null)
            {
                driver = proxy.AddComponent<SpeedFollowProxyDriver>();
            }

            Rigidbody bikeRb = bike.GetComponent<Rigidbody>();
            ConfigureDriver(driver, bike.transform, bikeRb);
            bool simpleFollowConfigured = ConfigureSimpleFollowCamera(bike.transform, bikeRb);

            bool cinemachineConfigured = ConfigureCinemachine(proxy.transform, bike.transform);
            if (!cinemachineConfigured)
            {
                Debug.LogWarning("[IsoCameraSetup] Cinemachine camera not found; RunScene camera settings were not updated.");
            }

            EditorSceneManager.MarkSceneDirty(runScene);
            EditorSceneManager.SaveScene(runScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoCameraSetup] Applied isometric follow in " + scenePath +
                      ", SimpleFollowConfigured=" + simpleFollowConfigured +
                      ", CinemachineConfigured=" + cinemachineConfigured);
        }

        private static string ResolveRunScenePath()
        {
            if (File.Exists(DefaultRunScenePath))
            {
                return DefaultRunScenePath;
            }

            string[] matches = AssetDatabase.FindAssets("t:Scene RunScene");
            if (matches != null && matches.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(matches[0]);
            }

            matches = AssetDatabase.FindAssets("t:Scene Run");
            if (matches != null && matches.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(matches[0]);
            }

            return string.Empty;
        }

        private static void ConfigureDriver(SpeedFollowProxyDriver driver, Transform target, Rigidbody targetRb)
        {
            SerializedObject so = new SerializedObject(driver);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("targetRb").objectReferenceValue = targetRb;
            so.FindProperty("useWorldOffset").boolValue = true;
            so.FindProperty("baseWorldOffset").vector3Value = new Vector3(-10f, 10f, -10f);
            so.FindProperty("extraBackAtHighSpeed").floatValue = 6f;
            so.FindProperty("speedLagStart").floatValue = 14f;
            so.FindProperty("speedLagEnd").floatValue = 28f;
            so.FindProperty("followSharpnessLowSpeed").floatValue = 16f;
            so.FindProperty("followSharpnessHighSpeed").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ConfigureSimpleFollowCamera(Transform target, Rigidbody targetRb)
        {
            Camera mainCamera = EnsureMainCamera();
            if (mainCamera == null)
            {
                return false;
            }

            SimpleFollowCamera follow = mainCamera.GetComponent<SimpleFollowCamera>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<SimpleFollowCamera>();
            }

            SerializedObject so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("targetRb").objectReferenceValue = targetRb;
            so.FindProperty("heightOffset").floatValue = 10f;
            so.FindProperty("followDistance").floatValue = 9f;
            so.FindProperty("smoothTime").floatValue = 0.15f;
            so.FindProperty("pitchAngle").floatValue = 35f;
            so.FindProperty("yawFollowStrength").floatValue = 0.88f;
            so.FindProperty("yawSmoothTime").floatValue = 0.12f;
            so.FindProperty("extraBackAtHighSpeed").floatValue = 6f;
            so.FindProperty("speedLagStart").floatValue = 14f;
            so.FindProperty("speedLagEnd").floatValue = 28f;
            so.FindProperty("followSharpnessLowSpeed").floatValue = 16f;
            so.FindProperty("followSharpnessHighSpeed").floatValue = 5f;
            so.FindProperty("preventClipping").boolValue = true;
            so.FindProperty("obstacleMask").intValue = ~0;
            so.FindProperty("occlusionPivotHeight").floatValue = 1.4f;
            so.FindProperty("collisionRadius").floatValue = 0.48f;
            so.FindProperty("collisionBuffer").floatValue = 0.25f;
            so.FindProperty("minDistanceFromTarget").floatValue = 2.2f;
            so.FindProperty("collisionBackoffStep").floatValue = 0.4f;
            so.FindProperty("collisionResolveSteps").intValue = 10;
            so.FindProperty("nearClipWhenOccluded").floatValue = 0f;
            so.FindProperty("defaultNearClip").floatValue = 0f;
            so.FindProperty("autoFindPlayer").boolValue = true;
            so.FindProperty("playerTag").stringValue = "Player";
            so.ApplyModifiedPropertiesWithoutUndo();

            follow.enabled = true;

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 12f;
            mainCamera.nearClipPlane = 0f;
            mainCamera.farClipPlane = 5000f;

            Type brainType = FindType(
                "Cinemachine.CinemachineBrain",
                "Unity.Cinemachine.CinemachineBrain");
            if (brainType != null)
            {
                Behaviour brain = mainCamera.GetComponent(brainType) as Behaviour;
                if (brain != null)
                {
                    // Keep simple-follow authoritative to avoid camera conflicts in scenes
                    // where Cinemachine pipeline components are incomplete.
                    brain.enabled = false;
                }
            }

            return true;
        }

        private static bool ConfigureCinemachine(Transform follow, Transform lookAt)
        {
            Type brainType = FindType(
                "Cinemachine.CinemachineBrain",
                "Unity.Cinemachine.CinemachineBrain");
            Type vcamType = FindType(
                "Cinemachine.CinemachineVirtualCamera",
                "Unity.Cinemachine.CinemachineCamera",
                "Cinemachine.CinemachineCamera");
            if (brainType == null || vcamType == null)
            {
                return false;
            }

            Camera mainCamera = EnsureMainCamera();
            if (mainCamera == null)
            {
                return false;
            }

            if (mainCamera.GetComponent(brainType) == null)
            {
                mainCamera.gameObject.AddComponent(brainType);
            }

            Component vcam = FindSceneComponentByType(vcamType);
            if (vcam == null)
            {
                GameObject vcamObject = new GameObject("RunIsoVCam");
                vcam = vcamObject.AddComponent(vcamType);
            }

            SetObjectMember(vcam, "Follow", follow);
            SetObjectMember(vcam, "m_Follow", follow);
            SetObjectMember(vcam, "LookAt", lookAt);
            SetObjectMember(vcam, "m_LookAt", lookAt);
            SetNumericMember(vcam, "Priority", 10f);
            SetNumericMember(vcam, "m_Priority", 10f);
            TrySetLensOrtho(vcam, 12f);
            TryZeroFollowOffsets(vcam);
            EditorUtility.SetDirty(vcam.gameObject);
            return true;
        }

        private static Camera EnsureMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras != null && cameras.Length > 0)
            {
                return cameras[0];
            }

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            return cameraGo.AddComponent<Camera>();
        }

        private static Component FindSceneComponentByType(Type type)
        {
            Object[] found = Resources.FindObjectsOfTypeAll(type);
            for (int i = 0; i < found.Length; i++)
            {
                Component component = found[i] as Component;
                if (component == null)
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(component))
                {
                    continue;
                }

                if (!component.gameObject.scene.IsValid())
                {
                    continue;
                }

                return component;
            }

            return null;
        }

        private static Type FindType(params string[] typeNames)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < typeNames.Length; i++)
            {
                string typeName = typeNames[i];
                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type t = assemblies[a].GetType(typeName, false);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static void SetObjectMember(object target, string memberName, Object value)
        {
            if (target == null)
            {
                return;
            }

            Type type = target.GetType();
            PropertyInfo prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && typeof(Object).IsAssignableFrom(prop.PropertyType))
            {
                prop.SetValue(target, value);
                return;
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && typeof(Object).IsAssignableFrom(field.FieldType))
            {
                field.SetValue(target, value);
            }
        }

        private static void SetNumericMember(object target, string memberName, float value)
        {
            if (target == null)
            {
                return;
            }

            Type type = target.GetType();
            PropertyInfo prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                if (prop.PropertyType == typeof(float))
                {
                    prop.SetValue(target, value);
                    return;
                }

                if (prop.PropertyType == typeof(int))
                {
                    prop.SetValue(target, Mathf.RoundToInt(value));
                    return;
                }
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                return;
            }

            if (field.FieldType == typeof(float))
            {
                field.SetValue(target, value);
            }
            else if (field.FieldType == typeof(int))
            {
                field.SetValue(target, Mathf.RoundToInt(value));
            }
        }

        private static void TrySetLensOrtho(Component vcam, float orthoSize)
        {
            if (vcam == null)
            {
                return;
            }

            Type type = vcam.GetType();
            FieldInfo lensField = type.GetField("m_Lens", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (lensField != null)
            {
                object lens = lensField.GetValue(vcam);
                if (SetLensStructValues(lens, true, orthoSize))
                {
                    lensField.SetValue(vcam, lens);
                    return;
                }
            }

            PropertyInfo lensProp = type.GetProperty("Lens", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (lensProp != null && lensProp.CanRead && lensProp.CanWrite)
            {
                object lens = lensProp.GetValue(vcam, null);
                if (SetLensStructValues(lens, true, orthoSize))
                {
                    lensProp.SetValue(vcam, lens, null);
                }
            }
        }

        private static bool SetLensStructValues(object lensStruct, bool ortho, float orthoSize)
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
                orthoField.SetValue(lensStruct, ortho);
                changed = true;
            }
            else
            {
                PropertyInfo orthoProp = lensType.GetProperty("Orthographic", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (orthoProp != null && orthoProp.CanWrite && orthoProp.PropertyType == typeof(bool))
                {
                    orthoProp.SetValue(lensStruct, ortho, null);
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

            return changed;
        }

        private static void TryZeroFollowOffsets(Component vcam)
        {
            if (vcam == null)
            {
                return;
            }

            Component[] components = vcam.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component comp = components[i];
                if (comp == null)
                {
                    continue;
                }

                Type type = comp.GetType();
                FieldInfo field = type.GetField("m_FollowOffset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(Vector3))
                {
                    field.SetValue(comp, Vector3.zero);
                    continue;
                }

                PropertyInfo prop = type.GetProperty("FollowOffset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(Vector3))
                {
                    prop.SetValue(comp, Vector3.zero, null);
                }
            }
        }
    }
}
