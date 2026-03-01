using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed partial class TrafficSignalVisualRuntime
    {
        private SignalVisual CreateSignalVisual(int nodeId, Vector3 intersectionPosition, Transform parent, GameObject signalTemplate)
        {
            GameObject signalRoot = new GameObject("Signal_" + nodeId.ToString());
            signalRoot.transform.SetParent(parent, false);
            signalRoot.transform.position = ResolveSignalRoadsidePosition(nodeId, intersectionPosition);

            float mastTopY = 3.0f;
            if (!TryInstantiateSignalBody(signalRoot.transform, signalTemplate, out mastTopY))
            {
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(signalRoot.transform, false);
                pole.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                pole.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
                DestroyColliderIfExists(pole);
                SetRendererMaterial(pole, _offMat);
            }

            GameObject verticalHead = new GameObject("HeadVertical");
            verticalHead.transform.SetParent(signalRoot.transform, false);
            verticalHead.transform.localPosition = new Vector3(-0.34f, mastTopY, 0f);

            GameObject horizontalHead = new GameObject("HeadHorizontal");
            horizontalHead.transform.SetParent(signalRoot.transform, false);
            horizontalHead.transform.localPosition = new Vector3(0.34f, mastTopY, 0f);

            SignalVisual visual = new SignalVisual
            {
                NodeId = nodeId,
                VerticalRed = CreateLamp(verticalHead.transform, "Vertical_Red", new Vector3(0f, 0.22f, 0f)),
                VerticalYellow = CreateLamp(verticalHead.transform, "Vertical_Yellow", new Vector3(0f, 0f, 0f)),
                VerticalGreen = CreateLamp(verticalHead.transform, "Vertical_Green", new Vector3(0f, -0.22f, 0f)),
                HorizontalRed = CreateLamp(horizontalHead.transform, "Horizontal_Red", new Vector3(0f, 0.22f, 0f)),
                HorizontalYellow = CreateLamp(horizontalHead.transform, "Horizontal_Yellow", new Vector3(0f, 0f, 0f)),
                HorizontalGreen = CreateLamp(horizontalHead.transform, "Horizontal_Green", new Vector3(0f, -0.22f, 0f))
            };

            ApplyPhaseVisual(ref visual, TrafficSignalPhase.VerticalGreen);
            return visual;
        }

        private static Vector3 ResolveSignalRoadsidePosition(int nodeId, Vector3 intersectionPosition)
        {
            int corner = nodeId & 3;
            float signX = 1f;
            float signZ = 1f;
            if (corner == 1)
            {
                signX = -1f;
            }
            else if (corner == 2)
            {
                signX = -1f;
                signZ = -1f;
            }
            else if (corner == 3)
            {
                signZ = -1f;
            }

            return new Vector3(
                intersectionPosition.x + (signX * SignalCornerOffset),
                intersectionPosition.y + SignalBaseYOffset,
                intersectionPosition.z + (signZ * SignalCornerOffset));
        }

        private bool TryInstantiateSignalBody(Transform parent, GameObject signalTemplate, out float mastTopY)
        {
            mastTopY = 3.0f;
            if (parent == null || signalTemplate == null)
            {
                return false;
            }

            GameObject body = Object.Instantiate(signalTemplate, parent);
            if (body == null)
            {
                return false;
            }

            body.name = "SignalBody";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            Collider[] colliders = body.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    Object.Destroy(colliders[i]);
                }
            }

            Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float localTop = bounds.max.y - parent.position.y;
            mastTopY = Mathf.Max(2.6f, localTop - 0.45f);
            return true;
        }

        private Renderer CreateLamp(Transform parent, string name, Vector3 localPos)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = name;
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPos;
            lamp.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            DestroyColliderIfExists(lamp);

            Renderer renderer = lamp.GetComponent<Renderer>();
            renderer.sharedMaterial = _offMat;
            return renderer;
        }

        private void ApplyPhaseVisual(ref SignalVisual visual, TrafficSignalPhase phase)
        {
            bool verticalGreen = phase == TrafficSignalPhase.VerticalGreen;
            bool verticalYellow = phase == TrafficSignalPhase.VerticalYellow;
            bool horizontalGreen = phase == TrafficSignalPhase.HorizontalGreen;
            bool horizontalYellow = phase == TrafficSignalPhase.HorizontalYellow;

            bool verticalRed = !verticalGreen && !verticalYellow;
            bool horizontalRed = !horizontalGreen && !horizontalYellow;

            SetLampActive(visual.VerticalRed, verticalRed, _redMat);
            SetLampActive(visual.VerticalYellow, verticalYellow, _yellowMat);
            SetLampActive(visual.VerticalGreen, verticalGreen, _greenMat);

            SetLampActive(visual.HorizontalRed, horizontalRed, _redMat);
            SetLampActive(visual.HorizontalYellow, horizontalYellow, _yellowMat);
            SetLampActive(visual.HorizontalGreen, horizontalGreen, _greenMat);
        }

        private void SetLampActive(Renderer renderer, bool active, Material activeMat)
        {
            if (renderer == null)
            {
                return;
            }

            Material target = active ? activeMat : _offMat;
            if (renderer.sharedMaterial != target)
            {
                renderer.sharedMaterial = target;
            }
        }
    }
}
