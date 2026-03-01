using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal static class FuelStationInteractVisualFactory
    {
        private static Material s_fuelInteractMaterial;

        internal static void AddInteractVisual(Transform parent)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f);

            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color tint = new Color(1f, 0.9f, 0.2f, 1f);
                renderer.sharedMaterial = GetFuelInteractMaterial(tint);
            }
        }

        private static Material GetFuelInteractMaterial(Color tint)
        {
            if (s_fuelInteractMaterial != null)
            {
                return s_fuelInteractMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = "FuelInteract_Opaque";
            material.SetColor("_Color", tint);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            }

            s_fuelInteractMaterial = material;
            return material;
        }
    }
}
