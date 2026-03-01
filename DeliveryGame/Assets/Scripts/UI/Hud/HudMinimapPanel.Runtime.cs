using System;
using DeliveryRun.Delivery.Orders;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Delivery.World;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class HudMinimapPanel
    {
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
    }
}
