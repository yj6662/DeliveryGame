using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Editor
{
    public static class DeliveryRunCurbSolidColliderFix
    {
        private const string RunScenePath = "Assets/Scenes/Run/RunScene.unity";
        private const float CurbBarrierTopMinY = 1.6f;
        private const float CurbBarrierBottomY = -4.0f;

        public static void GenerateAll()
        {
            Scene scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException("[CurbFix] Failed to open RunScene: " + RunScenePath);
            }

            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int fixedCount = 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                {
                    continue;
                }

                string name = t.name;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (!name.StartsWith("BlockCurb_", StringComparison.Ordinal))
                {
                    continue;
                }

                GameObject go = t.gameObject;
                Collider col = go.GetComponent<Collider>();
                if (col != null && !(col is BoxCollider))
                {
                    UnityEngine.Object.DestroyImmediate(col);
                }

                BoxCollider box = go.GetComponent<BoxCollider>();
                if (box == null)
                {
                    box = go.AddComponent<BoxCollider>();
                }

                float currentTopY = t.position.y + (Mathf.Abs(t.localScale.y) * 0.5f);
                float targetTopY = Mathf.Max(currentTopY, CurbBarrierTopMinY);
                float targetHeight = Mathf.Max(0.5f, targetTopY - CurbBarrierBottomY);
                float targetCenterY = CurbBarrierBottomY + (targetHeight * 0.5f);

                Vector3 localScale = t.localScale;
                localScale.y = targetHeight;
                t.localScale = localScale;
                t.position = new Vector3(t.position.x, targetCenterY, t.position.z);

                box.isTrigger = false;
                box.size = Vector3.one;
                box.center = Vector3.zero;

                fixedCount++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CurbFix] Applied solid curb colliders: " + fixedCount);
        }
    }
}
