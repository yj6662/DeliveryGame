using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeliveryRun.Delivery.Input
{
    public static class RuntimeInput
    {
        private static bool _legacyInputUnavailable;
#if ENABLE_INPUT_SYSTEM
        private static bool _initialized;
        private static InputActionMap _gameplayMap;
        private static InputAction _move;
        private static InputAction _offerAccept;
        private static InputAction _interact;
#endif

        public static Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureInitialized();
            if (_move != null)
            {
                return Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            }
#endif

            if (_legacyInputUnavailable)
            {
                return Vector2.zero;
            }

            try
            {
                float x = Mathf.Clamp(UnityEngine.Input.GetAxisRaw("Horizontal"), -1f, 1f);
                float y = Mathf.Clamp(UnityEngine.Input.GetAxisRaw("Vertical"), -1f, 1f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
            catch (InvalidOperationException)
            {
                _legacyInputUnavailable = true;
                return Vector2.zero;
            }
        }

        public static bool WasOfferAcceptPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureInitialized();
            if (_offerAccept != null && _offerAccept.WasPressedThisFrame())
            {
                return true;
            }
#endif

            if (_legacyInputUnavailable)
            {
                return false;
            }

            try
            {
                return UnityEngine.Input.GetKeyDown(KeyCode.Space);
            }
            catch (InvalidOperationException)
            {
                _legacyInputUnavailable = true;
                return false;
            }
        }

        public static bool WasInteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureInitialized();
            if (_interact != null && _interact.WasPressedThisFrame())
            {
                return true;
            }
#endif
            if (_legacyInputUnavailable)
            {
                return false;
            }

            try
            {
                return UnityEngine.Input.GetKeyDown(KeyCode.F);
            }
            catch (InvalidOperationException)
            {
                _legacyInputUnavailable = true;
                return false;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _gameplayMap = new InputActionMap("DeliveryRunGameplay");

            _move = _gameplayMap.AddAction("Move", InputActionType.Value);
            _move.expectedControlType = "Vector2";
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _offerAccept = _gameplayMap.AddAction("OfferAccept", InputActionType.Button);
            _offerAccept.AddBinding("<Keyboard>/space");
            _offerAccept.AddBinding("<Gamepad>/start");

            _interact = _gameplayMap.AddAction("Interact", InputActionType.Button);
            _interact.AddBinding("<Keyboard>/f");
            _interact.AddBinding("<Gamepad>/buttonSouth");

            _gameplayMap.Enable();
            _initialized = true;
        }
#endif
    }
}
