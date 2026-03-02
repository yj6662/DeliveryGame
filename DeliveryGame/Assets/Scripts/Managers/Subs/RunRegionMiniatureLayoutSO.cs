using System;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    [CreateAssetMenu(
        fileName = "RunRegionMiniatureLayout",
        menuName = "DeliveryRun/Run Region Miniature Layout")]
    public sealed class RunRegionMiniatureLayoutSO : ScriptableObject
    {
        [Serializable]
        public struct RegionLayout
        {
            public string RegionId;
            public Vector3 LocalPosition;
            public Vector3 LocalEuler;
            public float LocalScale;
            public int BuildingChildLimit;
        }

        [Serializable]
        public struct CameraLayout
        {
            public Vector3 LocalPosition;
            public Vector3 LookAtOffset;
            public float FieldOfView;
            public Rect Viewport;
        }

        [Serializable]
        public struct UiLayout
        {
            public Vector2 PanelAnchoredPosition;
            public Vector2 PanelSize;
        }

        [SerializeField] private RegionLayout[] regions = new RegionLayout[0];
        [SerializeField] private CameraLayout camera = new CameraLayout
        {
            LocalPosition = new Vector3(0f, 86f, -144f),
            LookAtOffset = new Vector3(0f, 4f, 0f),
            FieldOfView = 27f,
            Viewport = new Rect(0.62f, 0.03f, 0.35f, 0.34f)
        };
        [SerializeField] private UiLayout ui = new UiLayout
        {
            PanelAnchoredPosition = new Vector2(-18f, 18f),
            PanelSize = new Vector2(560f, 236f)
        };

        public CameraLayout Camera => camera;
        public UiLayout Ui => ui;

        public bool TryGetRegionLayout(string regionId, out RegionLayout layout)
        {
            if (!string.IsNullOrEmpty(regionId) && regions != null)
            {
                for (int i = 0; i < regions.Length; i++)
                {
                    if (string.Equals(regions[i].RegionId, regionId, StringComparison.Ordinal))
                    {
                        layout = regions[i];
                        return true;
                    }
                }
            }

            layout = default;
            return false;
        }
    }
}
