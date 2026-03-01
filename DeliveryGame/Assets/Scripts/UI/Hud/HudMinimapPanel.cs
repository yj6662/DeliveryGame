using DeliveryRun.Delivery.Orders;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class HudMinimapPanel
    {
        private const int OrderMinimapMarkerSlots = 3;
        private const int GasMinimapMarkerIndex = 3;
        private const int MaxMinimapMarkers = 4;
        private const float MinimapCameraHeight = 80f;
        private const float MinimapOrthographicSize = 70f;
        private const int MinimapTextureSize = 512;
        private const float MinimapPanelSize = 312f; // 208 * 1.5
        private const float MinimapPanelOffset = 36f;
        private const float MinimapInnerPadding = 12f;
        private const float MinimapMarkerPadding = 12f;
        private const float GasAnchorSearchInterval = 1f;

        private readonly bool[] _objectiveMarkerActive = new bool[MaxMinimapMarkers];
        private readonly Vector3[] _objectiveMarkerWorld = new Vector3[MaxMinimapMarkers];
        private readonly OrderPointType[] _objectiveMarkerPointTypes = new OrderPointType[MaxMinimapMarkers];
        private readonly RectTransform[] _objectiveDotRects = new RectTransform[MaxMinimapMarkers];
        private readonly RectTransform[] _objectiveEdgeRects = new RectTransform[MaxMinimapMarkers];
        private readonly Image[] _objectiveDots = new Image[MaxMinimapMarkers];
        private readonly Image[] _objectiveEdgeBadges = new Image[MaxMinimapMarkers];
        private readonly Text[] _objectiveEdgeTexts = new Text[MaxMinimapMarkers];

        private OrderBuildingAnchor _gasStationAnchor;
        private float _gasAnchorSearchAccum;

        private Camera _minimapCamera;
        private RenderTexture _minimapRt;
        private Sprite _circleMaskSprite;
        private RectTransform _minimapMaskRect;
        private RawImage _minimapRawImage;
        private bool _minimapUnsupportedLogged;

        internal HudMinimapPanel()
        {
            ClearObjectiveMarkers();
        }

        internal void ResetRuntimeState()
        {
            _gasStationAnchor = null;
            _gasAnchorSearchAccum = 0f;
            _minimapUnsupportedLogged = false;
            ClearObjectiveMarkers();
        }

        internal void Cleanup()
        {
            if (_minimapCamera != null)
            {
                _minimapCamera.targetTexture = null;
                Object.Destroy(_minimapCamera.gameObject);
                _minimapCamera = null;
            }

            if (_minimapRt != null)
            {
                _minimapRt.Release();
                Object.Destroy(_minimapRt);
                _minimapRt = null;
            }

            if (_circleMaskSprite != null)
            {
                Texture2D texture = _circleMaskSprite.texture;
                Object.Destroy(_circleMaskSprite);
                _circleMaskSprite = null;
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }

            _minimapMaskRect = null;
            _minimapRawImage = null;

            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                _objectiveDotRects[i] = null;
                _objectiveEdgeRects[i] = null;
                _objectiveDots[i] = null;
                _objectiveEdgeBadges[i] = null;
                _objectiveEdgeTexts[i] = null;
            }
        }

    }
}
