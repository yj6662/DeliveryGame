using System;
using DeliveryRun.Delivery.Input;
using DeliveryRun.Delivery.Vehicle;
using UnityEngine;

namespace DeliveryRun.Delivery.Orders
{
    [DisallowMultipleComponent]
    public sealed class OrderInteractPoint : MonoBehaviour
    {
        [SerializeField] private OrderPointType pointType = OrderPointType.Pickup;
        [SerializeField] private int orderId;
        [SerializeField] private string pointId = "P1";
        [SerializeField] private string displayName = "ORDER POINT";
        [SerializeField] private float interactRadius = 4f;

        private SphereCollider _trigger;
        private bool _playerInRange;
        private bool _armed;
        private bool _fired;

        public event Action<OrderInteractPoint> InteractRequested;

        public OrderPointType PointType => pointType;
        public int OrderId => orderId;
        public string PointId => pointId;
        public string DisplayName => displayName;

        public void Configure(OrderPointType type, string id, string name)
        {
            pointType = type;
            pointId = id;
            displayName = name;
        }

        public void Arm(int armedOrderId, OrderPointType armedPointType)
        {
            orderId = armedOrderId;
            pointType = armedPointType;
            _armed = true;
            _fired = false;
            _playerInRange = false;
            EnsureTriggerCollider();
            _trigger.enabled = true;
            enabled = true;
            gameObject.SetActive(true);
        }

        public void SetInteractRadius(float radius)
        {
            interactRadius = Mathf.Max(0.5f, radius);
            EnsureTriggerCollider();
            _trigger.radius = interactRadius;
        }

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        private void Update()
        {
            if (!_armed || _fired || !_playerInRange)
            {
                return;
            }

            if (!RuntimeInput.WasInteractPressedThisFrame())
            {
                return;
            }

            FireInteractRequested();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            _playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            _playerInRange = false;
        }

        private void FireInteractRequested()
        {
            _fired = true;
            _armed = false;
            _playerInRange = false;
            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            Action<OrderInteractPoint> callback = InteractRequested;
            if (callback != null)
            {
                callback(this);
            }
        }

        private void EnsureTriggerCollider()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<SphereCollider>();
            }

            if (_trigger == null)
            {
                _trigger = gameObject.AddComponent<SphereCollider>();
            }

            _trigger.isTrigger = true;
            _trigger.radius = Mathf.Max(0.5f, interactRadius);
            _trigger.center = Vector3.zero;
        }

        private static bool IsPlayerCollider(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.GetComponentInParent<MotorbikeController>() != null)
            {
                return true;
            }

            if (other.CompareTag("Player"))
            {
                return true;
            }

            Rigidbody attached = other.attachedRigidbody;
            if (attached != null && attached.CompareTag("Player"))
            {
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = pointType == OrderPointType.Pickup
                ? new Color(0.2f, 0.9f, 0.3f, 0.95f)
                : new Color(0.25f, 0.48f, 1f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.5f, interactRadius));
        }
#endif
    }
}
