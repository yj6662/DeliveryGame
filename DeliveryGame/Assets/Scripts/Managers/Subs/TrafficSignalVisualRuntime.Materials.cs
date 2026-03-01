using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class TrafficSignalVisualRuntime
    {
        private void EnsureMaterials()
        {
            if (_offMat != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _offMat = CreateMaterial(shader, new Color(0.12f, 0.12f, 0.12f, 1f), 0f);
            _redMat = CreateMaterial(shader, new Color(0.90f, 0.10f, 0.10f, 1f), 0f);
            _yellowMat = CreateMaterial(shader, new Color(1.00f, 0.85f, 0.10f, 1f), 0f);
            _greenMat = CreateMaterial(shader, new Color(0.10f, 0.85f, 0.20f, 1f), 0f);
        }

        private Material CreateMaterial(Shader shader, Color color, float metallic)
        {
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.2f);
            }

            return mat;
        }

        private void DestroyMaterials()
        {
            if (_offMat != null)
            {
                Object.Destroy(_offMat);
                _offMat = null;
            }

            if (_redMat != null)
            {
                Object.Destroy(_redMat);
                _redMat = null;
            }

            if (_yellowMat != null)
            {
                Object.Destroy(_yellowMat);
                _yellowMat = null;
            }

            if (_greenMat != null)
            {
                Object.Destroy(_greenMat);
                _greenMat = null;
            }
        }

        private static void DestroyColliderIfExists(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.Destroy(col);
            }
        }

        private static void SetRendererMaterial(GameObject go, Material mat)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
        }
    }
}
