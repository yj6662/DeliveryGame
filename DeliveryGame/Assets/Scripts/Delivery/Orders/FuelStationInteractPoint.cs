using System;
using DeliveryRun.Delivery.Vehicle;
using UnityEngine;

namespace DeliveryRun.Delivery.Orders
{
    [DisallowMultipleComponent]
    public sealed class FuelStationInteractPoint : MonoBehaviour
    {
        [SerializeField] private string displayName = "FUEL STATION";
        [SerializeField] private float interactRadius = 4f;

        private SphereCollider _trigger;
        private WorldSpaceInteractPrompt _prompt;
        private bool _playerInRange;
        private bool _armed;

        public event Action<FuelStationInteractPoint> InteractRequested;

        public bool IsPlayerInRange => _playerInRange;
        public bool IsArmed => _armed;
        public string DisplayName => displayName;

        public void Configure(string stationName, float radius)
        {
            if (!string.IsNullOrEmpty(stationName))
            {
                displayName = stationName;
            }

            interactRadius = Mathf.Max(1f, radius);
            EnsureTrigger();
            _trigger.radius = interactRadius;
            EnsurePrompt();
            UpdatePromptState();
        }

        public void Arm()
        {
            _armed = true;
            _playerInRange = false;
            EnsureTrigger();
            EnsurePrompt();
            UpdatePromptState();
            _trigger.enabled = true;
            enabled = true;
            gameObject.SetActive(true);
        }

        private void Awake()
        {
            EnsureTrigger();
            EnsurePrompt();
            UpdatePromptState();
        }

        public bool TryRequestInteract()
        {
            if (!_armed || !_playerInRange)
            {
                return false;
            }

            Action<FuelStationInteractPoint> callback = InteractRequested;
            if (callback == null)
            {
                return false;
            }

            callback(this);
            return true;
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

        private void EnsureTrigger()
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
            _trigger.radius = Mathf.Max(1f, interactRadius);
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

            if (_prompt != null)
            {
                _prompt.SetPrompt("Press F to Refuel");
            }
        }

        private void UpdatePromptState()
        {
            if (_prompt == null)
            {
                return;
            }

            _prompt.SetPrompt("Press F to Refuel");
            _prompt.SetVisible(_armed && _playerInRange);
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
    }
}
