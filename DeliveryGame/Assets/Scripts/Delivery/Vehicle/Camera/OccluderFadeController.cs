using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace DeliveryRun.Delivery.Vehicle
{
    /// <summary>
    /// Manages material alpha transitions for renderers that occlude the camera view.
    /// </summary>
    internal sealed class OccluderFadeController
    {
        private const float Epsilon = 0.0001f;

        private sealed class FadedOccluderState
        {
            public Material[] OriginalMaterials;
            public Material[] FadeMaterials;
        }

        private readonly Dictionary<Renderer, FadedOccluderState> _fadedOccluders = new Dictionary<Renderer, FadedOccluderState>(32);
        private readonly List<Renderer> _restoreBuffer = new List<Renderer>(32);
        private readonly List<Renderer> _frameOccluders = new List<Renderer>(16);

        public struct FadeParams
        {
            public bool FadeOccludingRenderers;
            public float OccluderFadeAlpha;
            public int MaxFadedOccluders;
            public bool UseRendererBoundsOcclusion;
            public float CollisionRadius;
            public LayerMask ObstacleMask;
        }

        public void UpdateOccluderFade(
            Vector3 pivot,
            Vector3 cameraPosition,
            Transform target,
            Transform cameraTransform,
            OcclusionSystem occlusion,
            OcclusionSystem.OcclusionParams occParams,
            FadeParams p)
        {
            if (!p.FadeOccludingRenderers)
            {
                RestoreAllFadedOccluders();
                return;
            }

            Vector3 toCamera = cameraPosition - pivot;
            float distance = toCamera.magnitude;
            if (distance <= Epsilon)
            {
                RestoreAllFadedOccluders();
                return;
            }

            Vector3 direction = toCamera / distance;
            _frameOccluders.Clear();

            if (p.UseRendererBoundsOcclusion && occlusion.CollectBoundsOccluders(pivot, direction, distance, target, cameraTransform, occParams))
            {
                int count = Mathf.Min(Mathf.Max(0, p.MaxFadedOccluders), occlusion.BoundsHits.Count);
                for (int i = 0; i < count; i++)
                {
                    Renderer renderer = occlusion.BoundsHits[i].Renderer;
                    if (renderer != null)
                    {
                        _frameOccluders.Add(renderer);
                    }
                }
            }
            else
            {
                if (Physics.SphereCast(
                    pivot,
                    Mathf.Max(0.02f, p.CollisionRadius),
                    direction,
                    out RaycastHit hit,
                    Mathf.Max(0f, distance - 0.01f),
                    p.ObstacleMask,
                    QueryTriggerInteraction.Ignore))
                {
                    Renderer renderer = hit.collider != null ? hit.collider.GetComponentInParent<Renderer>() : null;
                    if (renderer != null && OcclusionSystem.IsRendererUsable(renderer, target, cameraTransform, occParams))
                    {
                        _frameOccluders.Add(renderer);
                    }
                }
            }

            _restoreBuffer.Clear();
            foreach (KeyValuePair<Renderer, FadedOccluderState> kv in _fadedOccluders)
            {
                if (kv.Key == null || !_frameOccluders.Contains(kv.Key))
                {
                    _restoreBuffer.Add(kv.Key);
                }
            }

            for (int i = 0; i < _restoreBuffer.Count; i++)
            {
                RestoreOccluder(_restoreBuffer[i]);
            }

            for (int i = 0; i < _frameOccluders.Count; i++)
            {
                FadeOccluder(_frameOccluders[i], Mathf.Clamp01(p.OccluderFadeAlpha));
            }
        }

        public void RestoreAllFadedOccluders()
        {
            if (_fadedOccluders.Count == 0) return;

            _restoreBuffer.Clear();
            foreach (KeyValuePair<Renderer, FadedOccluderState> kv in _fadedOccluders)
            {
                _restoreBuffer.Add(kv.Key);
            }

            for (int i = 0; i < _restoreBuffer.Count; i++)
            {
                RestoreOccluder(_restoreBuffer[i]);
            }
        }

        private void FadeOccluder(Renderer renderer, float alpha)
        {
            if (renderer == null) return;

            if (!_fadedOccluders.TryGetValue(renderer, out FadedOccluderState state))
            {
                state = CreateFadedState(renderer, alpha);
                if (state == null) return;
                _fadedOccluders[renderer] = state;
            }

            Material[] fadeMaterials = state.FadeMaterials;
            if (fadeMaterials == null) return;

            for (int i = 0; i < fadeMaterials.Length; i++)
            {
                Material mat = fadeMaterials[i];
                if (mat == null) continue;
                SetMaterialAlpha(mat, alpha);
            }
        }

        private FadedOccluderState CreateFadedState(Renderer renderer, float alpha)
        {
            Material[] original = renderer.sharedMaterials;
            if (original == null || original.Length == 0) return null;

            Material[] fades = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                Material src = original[i];
                if (src == null) continue;

                Material inst = new Material(src);
                ConfigureTransparent(inst);
                SetMaterialAlpha(inst, alpha);
                fades[i] = inst;
            }

            renderer.sharedMaterials = fades;
            return new FadedOccluderState
            {
                OriginalMaterials = original,
                FadeMaterials = fades
            };
        }

        private void RestoreOccluder(Renderer renderer)
        {
            if (renderer == null) return;
            if (!_fadedOccluders.TryGetValue(renderer, out FadedOccluderState state)) return;

            if (renderer != null && state.OriginalMaterials != null)
            {
                renderer.sharedMaterials = state.OriginalMaterials;
            }

            DestroyFadeMaterials(state.FadeMaterials);
            _fadedOccluders.Remove(renderer);
        }

        private static void DestroyFadeMaterials(Material[] materials)
        {
            if (materials == null) return;
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat != null) Object.Destroy(mat);
            }
        }

        private static void ConfigureTransparent(Material mat)
        {
            if (mat == null) return;

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);

            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetMaterialAlpha(Material mat, float alpha)
        {
            if (mat == null) return;
            alpha = Mathf.Clamp01(alpha);

            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }

            if (mat.HasProperty("_Color"))
            {
                Color c = mat.GetColor("_Color");
                c.a = alpha;
                mat.SetColor("_Color", c);
            }
        }
    }
}
