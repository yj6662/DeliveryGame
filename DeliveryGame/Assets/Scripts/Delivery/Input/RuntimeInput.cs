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
        private static int _interactConsumedFrame = -1;
#if ENABLE_INPUT_SYSTEM
        private static bool _initialized;
        private static InputActionMap _gameplayMap;
        private static InputAction _move;
        private static InputAction _offerAccept;
        private static InputAction _interact;
        private static InputAction _submit;
        private static InputAction _digit1;
        private static InputAction _digit2;
        private static InputAction _digit3;
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

        public static bool ConsumeInteractPressedThisFrame()
        {
            int frame = Time.frameCount;
            if (_interactConsumedFrame == frame)
            {
                return false;
            }

            if (!WasInteractPressedThisFrame())
            {
                return false;
            }

            _interactConsumedFrame = frame;
            return true;
        }

        public static bool WasSubmitPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureInitialized();
            if (_submit != null && _submit.WasPressedThisFrame())
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
                return UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return);
            }
            catch (InvalidOperationException)
            {
                _legacyInputUnavailable = true;
                return false;
            }
        }

        public static int ReadMusicOptionIndexPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureInitialized();
            if (_digit1 != null && _digit1.WasPressedThisFrame()) return 0;
            if (_digit2 != null && _digit2.WasPressedThisFrame()) return 1;
            if (_digit3 != null && _digit3.WasPressedThisFrame()) return 2;
#endif
            if (_legacyInputUnavailable)
            {
                return -1;
            }

            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) return 0;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) return 1;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) return 2;
            }
            catch (InvalidOperationException)
            {
                _legacyInputUnavailable = true;
            }

            return -1;
        }

        public static bool TryReadPointerScreenPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            EnsureInitialized();
            if (Pointer.current != null)
            {
                screenPosition = Pointer.current.position.ReadValue();
                return true;
            }
#endif
            if (_legacyInputUnavailable)
            {
                screenPosition = Vector2.zero;
                return false;
            }

            try
            {
                screenPosition = UnityEngine.Input.mousePosition;
                return true;
            }
            catch (InvalidOperationException)
            {
                _legacyInputUnavailable = true;
                screenPosition = Vector2.zero;
                return false;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                if (_gameplayMap != null && !_gameplayMap.enabled)
                {
                    _gameplayMap.Enable();
                }
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

            _submit = _gameplayMap.AddAction("Submit", InputActionType.Button);
            _submit.AddBinding("<Keyboard>/space");
            _submit.AddBinding("<Keyboard>/enter");
            _submit.AddBinding("<Gamepad>/buttonSouth");

            _digit1 = _gameplayMap.AddAction("Digit1", InputActionType.Button);
            _digit1.AddBinding("<Keyboard>/1");
            _digit1.AddBinding("<Keyboard>/numpad1");

            _digit2 = _gameplayMap.AddAction("Digit2", InputActionType.Button);
            _digit2.AddBinding("<Keyboard>/2");
            _digit2.AddBinding("<Keyboard>/numpad2");

            _digit3 = _gameplayMap.AddAction("Digit3", InputActionType.Button);
            _digit3.AddBinding("<Keyboard>/3");
            _digit3.AddBinding("<Keyboard>/numpad3");

            _gameplayMap.Enable();
            _initialized = true;
        }
#endif
    }
}
