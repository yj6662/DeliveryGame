using System;
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
        private WorldSpaceInteractPrompt _prompt;
        private bool _playerInRange;
        private bool _armed;
        private bool _fired;

        public event Action<OrderInteractPoint> InteractRequested;

        public OrderPointType PointType => pointType;
        public int OrderId => orderId;
        public string PointId => pointId;
        public string DisplayName => displayName;
        public bool IsPlayerInRange => _playerInRange;
        public bool IsArmed => _armed && !_fired;

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
            EnsurePrompt();
            UpdatePromptState();
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

        public bool TryRequestInteract()
        {
            if (!_armed || _fired || !_playerInRange)
            {
                return false;
            }

            FireInteractRequested();
            return true;
        }

        private void Awake()
        {
            EnsureTriggerCollider();
            EnsurePrompt();
            UpdatePromptState();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            _playerInRange = true;
            UpdatePromptState();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            _playerInRange = false;
            UpdatePromptState();
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

            UpdatePromptState();

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

        private void EnsurePrompt()
        {
            if (_prompt == null)
            {
                _prompt = GetComponentInChildren<WorldSpaceInteractPrompt>(true);
            }

            if (_prompt == null)
            {
                _prompt = gameObject.AddComponent<WorldSpaceInteractPrompt>();
            }

            if (_prompt == null)
            {
                return;
            }

            _prompt.SetPrompt(BuildPromptText());
        }

        private string BuildPromptText()
        {
            if (pointType == OrderPointType.Delivery)
            {
                return "Press F to Deliver";
            }

            return "Press F to Pick Up";
        }

        private void UpdatePromptState()
        {
            if (_prompt == null)
            {
                return;
            }

            _prompt.SetPrompt(BuildPromptText());
            _prompt.SetVisible(_armed && !_fired && _playerInRange);
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
