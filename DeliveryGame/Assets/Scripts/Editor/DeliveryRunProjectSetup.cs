using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunProjectSetup
    {
        private const string CoreScenePath = "Assets/Scenes/Core/CoreScene.unity";
        private const string LobbyScenePath = "Assets/Scenes/Lobby/LobbyScene.unity";
        private const string LoadingScenePath = "Assets/Scenes/Loading/LoadingScene.unity";
        private const string RunScenePath = "Assets/Scenes/Run/RunScene.unity";

        [MenuItem("Tools/DeliveryRun/Setup Scenes & Build Settings")]
        public static void GenerateAll()
        {
            EnsureSceneFolders();
            CreateSceneIfMissing(CoreScenePath, createCoreRoot: true);
            CreateSceneIfMissing(LobbyScenePath, createCoreRoot: false);
            CreateSceneIfMissing(LoadingScenePath, createCoreRoot: false);
            CreateSceneIfMissing(RunScenePath, createCoreRoot: false);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(CoreScenePath, true),
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(LoadingScenePath, true),
                new EditorBuildSettingsScene(RunScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DeliveryRunProjectSetup] Scene setup complete.");
        }

        private static void EnsureSceneFolders()
        {
            Directory.CreateDirectory("Assets/Scenes/Core");
            Directory.CreateDirectory("Assets/Scenes/Lobby");
            Directory.CreateDirectory("Assets/Scenes/Loading");
            Directory.CreateDirectory("Assets/Scenes/Run");
        }

        private static void CreateSceneIfMissing(string scenePath, bool createCoreRoot)
        {
            if (File.Exists(scenePath))
            {
                Debug.Log("[DeliveryRunProjectSetup] Scene exists, skip: " + scenePath);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (createCoreRoot)
            {
                CreateCoreRootObject();
            }

            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            if (!saved)
            {
                Debug.LogError("[DeliveryRunProjectSetup] Failed to save scene: " + scenePath);
            }
            else
            {
                Debug.Log("[DeliveryRunProjectSetup] Created scene: " + scenePath);
            }
        }

        private static void CreateCoreRootObject()
        {
            GameObject coreRoot = new GameObject("CoreRoot");
            Type coreRootType = FindCoreRootType();
            if (coreRootType == null)
            {
                Debug.LogWarning(
                    "[DeliveryRunProjectSetup] CoreRoot type not found. Scene created without component.");
                return;
            }

            coreRoot.AddComponent(coreRootType);
        }

        private static Type FindCoreRootType()
        {
            const string fullTypeName = "DeliveryRun.Managers.Core.CoreRoot";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type candidate = assemblies[i].GetType(fullTypeName, false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
