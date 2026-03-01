using System;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI.Run;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudMinimapPanel
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

        internal void BuildIfNeeded(RunHudView view)
        {
            if (view == null || _minimapMaskRect != null)
            {
                return;
            }

            RectTransform panelRect = view.GetMinimapPanelRectTransform();
            Image maskImage = view.GetMinimapImage();
            if (panelRect == null || maskImage == null)
            {
                return;
            }

            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(MinimapPanelOffset, MinimapPanelOffset);
            panelRect.sizeDelta = new Vector2(MinimapPanelSize, MinimapPanelSize);

            Image panelImage = panelRect.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.08f, 0.1f, 0.13f, 0.88f);
                panelImage.raycastTarget = false;
            }

            if (_circleMaskSprite == null)
            {
                _circleMaskSprite = CreateCircleMaskSprite(256);
            }

            Mask mask = maskImage.GetComponent<Mask>();
            if (mask == null)
            {
                mask = maskImage.gameObject.AddComponent<Mask>();
            }

            mask.showMaskGraphic = true;
            maskImage.sprite = _circleMaskSprite;
            maskImage.type = Image.Type.Simple;
            maskImage.color = new Color(0.03f, 0.03f, 0.03f, 0.98f);
            maskImage.raycastTarget = false;

            _minimapMaskRect = maskImage.rectTransform;
            _minimapMaskRect.anchorMin = new Vector2(0f, 0f);
            _minimapMaskRect.anchorMax = new Vector2(1f, 1f);
            _minimapMaskRect.offsetMin = new Vector2(MinimapInnerPadding, MinimapInnerPadding);
            _minimapMaskRect.offsetMax = new Vector2(-MinimapInnerPadding, -MinimapInnerPadding);

            Transform mapRender = _minimapMaskRect.Find("MapRender");
            if (mapRender == null)
            {
                GameObject mapRenderObject = new GameObject("MapRender", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform mapRenderRect = mapRenderObject.GetComponent<RectTransform>();
                mapRenderRect.SetParent(_minimapMaskRect, false);
                mapRenderRect.anchorMin = Vector2.zero;
                mapRenderRect.anchorMax = Vector2.one;
                mapRenderRect.offsetMin = Vector2.zero;
                mapRenderRect.offsetMax = Vector2.zero;
                _minimapRawImage = mapRenderObject.GetComponent<RawImage>();
            }
            else
            {
                _minimapRawImage = mapRender.GetComponent<RawImage>();
                if (_minimapRawImage == null)
                {
                    _minimapRawImage = mapRender.gameObject.AddComponent<RawImage>();
                }
            }

            _minimapRawImage.raycastTarget = false;

            EnsureMinimapCamera();
            _minimapRawImage.texture = _minimapRt;
            _minimapRawImage.color = Color.white;

            RectTransform overlay = _minimapMaskRect.Find("Overlay") as RectTransform;
            if (overlay == null)
            {
                GameObject overlayObject = new GameObject("Overlay", typeof(RectTransform));
                overlay = overlayObject.GetComponent<RectTransform>();
                overlay.SetParent(_minimapMaskRect, false);
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = Vector2.zero;
                overlay.offsetMax = Vector2.zero;
            }

            Font defaultFont = view.GetDefaultFont();
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                Color markerColor = GetMarkerColorForType(_objectiveMarkerPointTypes[i]);
                float markerSize = i == 0 ? 16f : 13f;

                Image dot = HudUiFactory.EnsureMarkerImage(overlay, "ObjectiveDot_" + i, markerColor, markerSize);
                dot.gameObject.SetActive(false);
                _objectiveDots[i] = dot;
                _objectiveDotRects[i] = dot.rectTransform;

                Image edgeBadge = HudUiFactory.EnsureMarkerImage(overlay, "ObjectiveEdgeBadge_" + i, markerColor, i == 0 ? 18f : 16f);
                edgeBadge.sprite = _circleMaskSprite;
                edgeBadge.type = Image.Type.Simple;
                edgeBadge.gameObject.SetActive(false);
                _objectiveEdgeBadges[i] = edgeBadge;
                _objectiveEdgeRects[i] = edgeBadge.rectTransform;

                Text edgeText = EnsureMarkerText(edgeBadge.rectTransform, defaultFont, "Arrow", ">", new Color(0f, 0f, 0f, 1f), 14);
                edgeText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                edgeText.rectTransform.sizeDelta = new Vector2(18f, 18f);
                edgeText.rectTransform.anchoredPosition = Vector2.zero;
                edgeText.gameObject.SetActive(false);
                _objectiveEdgeTexts[i] = edgeText;

                ApplyMarkerColor(i);
            }

            Text playerArrow = EnsureMarkerText(overlay, defaultFont, "PlayerArrow", "^", new Color(0.25f, 1f, 0.8f, 1f), 24);
            playerArrow.rectTransform.anchoredPosition = Vector2.zero;
            playerArrow.rectTransform.localRotation = Quaternion.identity;
        }

        internal void Refresh(ref MotorbikeController player)
        {
            if (_minimapCamera == null || _minimapMaskRect == null)
            {
                return;
            }

            if (player == null)
            {
                player = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (player == null)
            {
                for (int i = 0; i < MaxMinimapMarkers; i++)
                {
                    SetMarkerHidden(i);
                }

                return;
            }

            RefreshGasStationMarker(player);

            Transform playerTransform = player.transform;
            float yaw = playerTransform.eulerAngles.y;
            _minimapCamera.transform.SetPositionAndRotation(
                playerTransform.position + Vector3.up * MinimapCameraHeight,
                Quaternion.Euler(90f, yaw, 0f));

            float uiRadius = Mathf.Min(_minimapMaskRect.rect.width, _minimapMaskRect.rect.height) * 0.5f - MinimapMarkerPadding;
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                if (_objectiveDots[i] == null || _objectiveEdgeRects[i] == null || _objectiveEdgeTexts[i] == null || _objectiveEdgeBadges[i] == null)
                {
                    continue;
                }

                if (!_objectiveMarkerActive[i])
                {
                    SetMarkerHidden(i);
                    continue;
                }

                ApplyMarkerColor(i);

                Vector3 delta = _objectiveMarkerWorld[i] - playerTransform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.0001f)
                {
                    SetMarkerHidden(i);
                    continue;
                }

                Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * delta;
                Vector2 flat = new Vector2(local.x, local.z);
                Vector2 uiPos = flat * (uiRadius / Mathf.Max(1f, _minimapCamera.orthographicSize));

                if (uiPos.magnitude <= uiRadius * 0.86f)
                {
                    _objectiveDots[i].gameObject.SetActive(true);
                    _objectiveEdgeBadges[i].gameObject.SetActive(false);
                    _objectiveEdgeTexts[i].gameObject.SetActive(false);
                    _objectiveDotRects[i].anchoredPosition = uiPos;
                    continue;
                }

                Vector2 dir = uiPos.normalized;
                _objectiveDots[i].gameObject.SetActive(false);
                _objectiveEdgeBadges[i].gameObject.SetActive(true);
                _objectiveEdgeTexts[i].gameObject.SetActive(true);
                _objectiveEdgeRects[i].anchoredPosition = dir * (uiRadius * 1.02f);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _objectiveEdgeRects[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        internal void SetObjectiveMarker(bool active, Vector3 worldPosition, OrderPointType pointType)
        {
            _objectiveMarkerActive[0] = active;
            _objectiveMarkerWorld[0] = worldPosition;
            _objectiveMarkerPointTypes[0] = pointType;
            ApplyMarkerColor(0);

            for (int i = 1; i < OrderMinimapMarkerSlots; i++)
            {
                _objectiveMarkerActive[i] = false;
                _objectiveMarkerWorld[i] = Vector3.zero;
                _objectiveMarkerPointTypes[i] = OrderPointType.Pickup;
                ApplyMarkerColor(i);
            }
        }

        internal void SetObjectiveMarkers(OrderObjectiveMarkersUpdated evt)
        {
            for (int i = 0; i < OrderMinimapMarkerSlots; i++)
            {
                _objectiveMarkerActive[i] = false;
                _objectiveMarkerWorld[i] = Vector3.zero;
                _objectiveMarkerPointTypes[i] = OrderPointType.Pickup;
                ApplyMarkerColor(i);
            }

            int count = evt.Count;
            if (count <= 0)
            {
                return;
            }

            _objectiveMarkerActive[0] = true;
            _objectiveMarkerWorld[0] = evt.WorldPosition0;
            _objectiveMarkerPointTypes[0] = evt.PointType0;
            ApplyMarkerColor(0);

            if (count > 1)
            {
                _objectiveMarkerActive[1] = true;
                _objectiveMarkerWorld[1] = evt.WorldPosition1;
                _objectiveMarkerPointTypes[1] = evt.PointType1;
                ApplyMarkerColor(1);
            }

            if (count > 2)
            {
                _objectiveMarkerActive[2] = true;
                _objectiveMarkerWorld[2] = evt.WorldPosition2;
                _objectiveMarkerPointTypes[2] = evt.PointType2;
                ApplyMarkerColor(2);
            }
        }

        internal void ClearObjectiveMarkers()
        {
            for (int i = 0; i < MaxMinimapMarkers; i++)
            {
                _objectiveMarkerActive[i] = false;
                _objectiveMarkerWorld[i] = Vector3.zero;
                _objectiveMarkerPointTypes[i] = OrderPointType.Pickup;
                SetMarkerHidden(i);
            }
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

        private void EnsureMinimapCamera()
        {
            bool graphicsUnsupported = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            if (graphicsUnsupported)
            {
                if (!_minimapUnsupportedLogged)
                {
                    _minimapUnsupportedLogged = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[UiRunHudManager] Minimap render texture disabled on unsupported graphics device.");
#endif
                }

                if (_minimapRawImage != null)
                {
                    _minimapRawImage.texture = null;
                    _minimapRawImage.gameObject.SetActive(false);
                }

                return;
            }

            _minimapUnsupportedLogged = false;

            if (_minimapCamera == null)
            {
                GameObject cameraObject = GameObject.Find("RunMiniMapCamera");
                if (cameraObject == null)
                {
                    cameraObject = new GameObject("RunMiniMapCamera");
                }

                _minimapCamera = cameraObject.GetComponent<Camera>();
                if (_minimapCamera == null)
                {
                    _minimapCamera = cameraObject.AddComponent<Camera>();
                }
            }

            _minimapCamera.orthographic = true;
            _minimapCamera.orthographicSize = MinimapOrthographicSize;
            _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            _minimapCamera.backgroundColor = new Color(0.18f, 0.2f, 0.22f, 1f);
            _minimapCamera.cullingMask = ~0;
            _minimapCamera.nearClipPlane = 0.01f;
            _minimapCamera.farClipPlane = 400f;
            _minimapCamera.depth = -80f;
            _minimapCamera.allowHDR = false;
            _minimapCamera.allowMSAA = false;

            if (_minimapRt == null || _minimapRt.width != MinimapTextureSize || _minimapRt.height != MinimapTextureSize)
            {
                if (_minimapRt != null)
                {
                    _minimapRt.Release();
                    Object.Destroy(_minimapRt);
                }

                _minimapRt = new RenderTexture(MinimapTextureSize, MinimapTextureSize, 16, RenderTextureFormat.ARGB32);
                _minimapRt.name = "RunMinimapRT";
                _minimapRt.useMipMap = false;
                _minimapRt.autoGenerateMips = false;
                _minimapRt.Create();
            }

            _minimapCamera.targetTexture = _minimapRt;
            if (_minimapRawImage != null)
            {
                _minimapRawImage.gameObject.SetActive(true);
            }
        }

        private void RefreshGasStationMarker(MotorbikeController player)
        {
            if (_gasStationAnchor != null && !_gasStationAnchor.gameObject.activeInHierarchy)
            {
                _gasStationAnchor = null;
            }

            if (player == null)
            {
                _objectiveMarkerActive[GasMinimapMarkerIndex] = false;
                _objectiveMarkerWorld[GasMinimapMarkerIndex] = Vector3.zero;
                _objectiveMarkerPointTypes[GasMinimapMarkerIndex] = OrderPointType.GasStation;
                SetMarkerHidden(GasMinimapMarkerIndex);
                return;
            }

            string currentRegionId = RegionWorldLayout.ResolveRegionId(player.transform.position);
            if (_gasStationAnchor != null &&
                (!string.Equals(_gasStationAnchor.RegionId, currentRegionId, StringComparison.Ordinal) ||
                 _gasStationAnchor.Role != OrderBuildingRole.GasStation))
            {
                _gasStationAnchor = null;
            }

            _gasAnchorSearchAccum += Time.unscaledDeltaTime;
            if (_gasStationAnchor == null && _gasAnchorSearchAccum >= GasAnchorSearchInterval)
            {
                _gasAnchorSearchAccum = 0f;
                OrderBuildingAnchor[] anchors = Object.FindObjectsByType<OrderBuildingAnchor>(FindObjectsSortMode.None);
                OrderBuildingAnchor fallback = null;
                for (int i = 0; i < anchors.Length; i++)
                {
                    OrderBuildingAnchor anchor = anchors[i];
                    if (anchor == null || anchor.Role != OrderBuildingRole.GasStation)
                    {
                        continue;
                    }

                    if (string.Equals(anchor.RegionId, currentRegionId, StringComparison.Ordinal))
                    {
                        _gasStationAnchor = anchor;
                        break;
                    }

                    if (fallback == null)
                    {
                        fallback = anchor;
                    }
                }

                if (_gasStationAnchor == null)
                {
                    _gasStationAnchor = fallback;
                }
            }

            if (_gasStationAnchor == null)
            {
                _objectiveMarkerActive[GasMinimapMarkerIndex] = false;
                _objectiveMarkerWorld[GasMinimapMarkerIndex] = Vector3.zero;
                _objectiveMarkerPointTypes[GasMinimapMarkerIndex] = OrderPointType.GasStation;
                SetMarkerHidden(GasMinimapMarkerIndex);
                return;
            }

            _objectiveMarkerActive[GasMinimapMarkerIndex] = true;
            _objectiveMarkerWorld[GasMinimapMarkerIndex] = _gasStationAnchor.transform.position;
            _objectiveMarkerPointTypes[GasMinimapMarkerIndex] = OrderPointType.GasStation;
            ApplyMarkerColor(GasMinimapMarkerIndex);
        }

        private void SetMarkerHidden(int index)
        {
            if (index < 0 || index >= MaxMinimapMarkers)
            {
                return;
            }

            if (_objectiveDots[index] != null)
            {
                _objectiveDots[index].gameObject.SetActive(false);
            }

            if (_objectiveEdgeBadges[index] != null)
            {
                _objectiveEdgeBadges[index].gameObject.SetActive(false);
            }

            if (_objectiveEdgeTexts[index] != null)
            {
                _objectiveEdgeTexts[index].gameObject.SetActive(false);
            }
        }

        private static Color GetMarkerColorForType(OrderPointType pointType)
        {
            if (pointType == OrderPointType.GasStation)
            {
                return new Color(0.35f, 1f, 0.45f, 0.98f);
            }

            if (pointType == OrderPointType.Delivery)
            {
                return new Color(0.22f, 0.78f, 1f, 0.98f);
            }

            return new Color(1f, 0.84f, 0.2f, 0.98f);
        }

        private void ApplyMarkerColor(int index)
        {
            if (index < 0 || index >= MaxMinimapMarkers)
            {
                return;
            }

            Color markerColor = GetMarkerColorForType(_objectiveMarkerPointTypes[index]);
            if (_objectiveDots[index] != null)
            {
                _objectiveDots[index].color = markerColor;
            }

            if (_objectiveEdgeBadges[index] != null)
            {
                _objectiveEdgeBadges[index].color = markerColor;
            }
        }

        private static Sprite CreateCircleMaskSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "MinimapCircleMask";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            float radiusSq = radius * radius;
            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 fill = new Color32(255, 255, 255, 255);

            Color32[] pixels = new Color32[size * size];
            int idx = 0;
            for (int y = 0; y < size; y++)
            {
                float dy = y - center;
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    pixels[idx++] = (dx * dx + dy * dy) <= radiusSq ? fill : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Text EnsureMarkerText(RectTransform parent, Font font, string name, string value, Color color, int fontSize)
        {
            Transform markerTransform = parent.Find(name);
            Text text;
            if (markerTransform == null)
            {
                GameObject markerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                RectTransform markerRect = markerObject.GetComponent<RectTransform>();
                markerRect.SetParent(parent, false);
                markerRect.sizeDelta = new Vector2(28f, 28f);
                text = markerObject.GetComponent<Text>();
            }
            else
            {
                text = markerTransform.GetComponent<Text>() ?? markerTransform.gameObject.AddComponent<Text>();
            }

            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }
    }
}
