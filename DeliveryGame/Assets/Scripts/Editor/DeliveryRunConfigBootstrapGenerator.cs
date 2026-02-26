using System.IO;
using DeliveryRun.Managers.Core;
using UnityEditor;
using UnityEngine;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunConfigBootstrapGenerator
    {
        private const string BootstrapFolder = "Assets/Resources/Bootstrap";
        private const string FoodConfigPath = BootstrapFolder + "/FoodStateConfig.asset";
        private const string RatingConfigPath = BootstrapFolder + "/RatingConfig.asset";

        [MenuItem("Tools/DeliveryRun/Generate Config Bootstrap Assets")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(BootstrapFolder);

            FoodStateConfigSO foodConfig = AssetDatabase.LoadAssetAtPath<FoodStateConfigSO>(FoodConfigPath);
            if (foodConfig == null)
            {
                foodConfig = ScriptableObject.CreateInstance<FoodStateConfigSO>();
                AssetDatabase.CreateAsset(foodConfig, FoodConfigPath);
            }

            RatingConfigSO ratingConfig = AssetDatabase.LoadAssetAtPath<RatingConfigSO>(RatingConfigPath);
            if (ratingConfig == null)
            {
                ratingConfig = ScriptableObject.CreateInstance<RatingConfigSO>();
                AssetDatabase.CreateAsset(ratingConfig, RatingConfigPath);
            }

            EditorUtility.SetDirty(foodConfig);
            EditorUtility.SetDirty(ratingConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ConfigGen] FoodStateConfig: " + FoodConfigPath);
            Debug.Log("[ConfigGen] RatingConfig: " + RatingConfigPath);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            parent = parent.Replace('\\', '/');
            string child = Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
