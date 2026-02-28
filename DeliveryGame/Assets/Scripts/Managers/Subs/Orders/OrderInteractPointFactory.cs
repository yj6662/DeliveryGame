using System;
using DeliveryRun.Delivery.Orders;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class OrderInteractPointFactory
    {
        private static Material s_pickupInteractMaterial;
        private static Material s_deliveryInteractMaterial;

        internal OrderInteractPoint Create(
            string objectName,
            int orderId,
            OrderPointType pointType,
            string pointId,
            string displayName,
            Vector3 spawnPosition,
            float interactRadius,
            Action<OrderInteractPoint> onInteractRequested)
        {
            GameObject interactObject = new GameObject(objectName);
            interactObject.transform.position = spawnPosition;
            interactObject.transform.rotation = Quaternion.identity;

            OrderInteractPoint interactPoint = interactObject.AddComponent<OrderInteractPoint>();
            interactPoint.Configure(pointType, pointId, displayName);
            interactPoint.SetInteractRadius(interactRadius);
            interactPoint.Arm(orderId, pointType);
            if (onInteractRequested != null)
            {
                interactPoint.InteractRequested += onInteractRequested;
            }

            AddInteractVisual(interactObject.transform, pointType);
            return interactPoint;
        }

        private static void AddInteractVisual(Transform parent, OrderPointType pointType)
        {
            Color tint = pointType == OrderPointType.Pickup
                ? new Color(0.2f, 0.9f, 0.3f, 1f)
                : new Color(0.2f, 0.45f, 1f, 1f);
            Material markerMaterial = GetInteractVisualMaterial(pointType, tint);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            visual.transform.localScale = new Vector3(1.6f, 0.08f, 1.6f);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Object.Destroy(visualCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = markerMaterial;
            }

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "VisualBeacon";
            beacon.transform.SetParent(parent, false);
            beacon.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            beacon.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            Collider beaconCollider = beacon.GetComponent<Collider>();
            if (beaconCollider != null)
            {
                Object.Destroy(beaconCollider);
            }

            Renderer beaconRenderer = beacon.GetComponent<Renderer>();
            if (beaconRenderer != null)
            {
                beaconRenderer.sharedMaterial = markerMaterial;
            }
        }

        private static Material GetInteractVisualMaterial(OrderPointType pointType, Color tint)
        {
            Material cached = pointType == OrderPointType.Pickup
                ? s_pickupInteractMaterial
                : s_deliveryInteractMaterial;
            if (cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = pointType == OrderPointType.Pickup
                ? "OrderInteract_Pickup_Opaque"
                : "OrderInteract_Delivery_Opaque";
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

            if (pointType == OrderPointType.Pickup)
            {
                s_pickupInteractMaterial = material;
            }
            else
            {
                s_deliveryInteractMaterial = material;
            }

            return material;
        }
    }
}
