using UnityEngine;
using DeliveryRun.Delivery.Input;

namespace DeliveryRun.Delivery.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class MotorbikeController : MonoBehaviour
    {
        [SerializeField] private Rigidbody rb;

        [Header("Legacy Wheel Refs (unused)")]
        [SerializeField] private Transform frontWheelRoot;
        [SerializeField] private Transform rearWheelRoot;

        [Header("Legacy Wheel Visuals (unused)")]
        [SerializeField] private Transform frontWheelVisual;
        [SerializeField] private Transform rearWheelVisual;

        [Header("Movement")]
        [SerializeField] private float maxMoveSpeed = 11f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float brakeForce = 16f;
        [SerializeField] private float reverseSpeed = 3.8f;
        [SerializeField] private float coastingDeceleration = 1.4f;
        [SerializeField] private float accelerationSmoothing = 0.10f;
        [SerializeField] private float reverseEnableSpeedThreshold = 0.5f;
        [SerializeField] private float reverseHoldSeconds = 0.12f;
        [SerializeField] private float reverseAccelerationMul = 1.15f;

        [Header("Steering")]
        [SerializeField] private float turnSpeed = 130f;
        [SerializeField] private float minSpeedToTurn = 0.5f;
        [Range(0f, 0.8f)]
        [SerializeField] private float highSpeedTurnPenalty = 0.4f;
        [Range(0f, 1f)]
        [SerializeField] private float lowSpeedSteeringScale = 0.7f;

        [Header("Lean")]
        [SerializeField] private Transform visualMesh;
        [SerializeField] private float maxLeanAngle = 22f;
        [SerializeField] private float leanSpeed = 8f;

        [Header("Drift")]
        [SerializeField] private bool enableDrift = true;
        [SerializeField] private float driftSpeedThreshold = 6f;
        [Range(0f, 1f)]
        [SerializeField] private float driftAmount = 0.28f;

        [Header("Physics")]
        [SerializeField] private float drag = 2f;
        [SerializeField] private float angularDrag = 5f;
        [SerializeField] private float downforce = 5f;

        [Header("Body")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);
        [SerializeField] private LayerMask groundMask = ~0;

        private float _steeringInput;
        private float _throttleInput;
        private float _currentSpeed;
        private float _targetSpeed;
        private float _throttleRampValue;
        private float _reverseHoldTimer;
        private float _currentLeanAngle;

        private float _gripMul = 1f;
        private float _brakeMul = 1f;

        public float SpeedMultiplier { get; private set; } = 1f;
        public float CurrentSpeed => _currentSpeed;
        public float EffectiveMaxSpeed => Mathf.Max(0f, maxMoveSpeed * SpeedMultiplier);

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.centerOfMass = centerOfMassOffset;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            if (visualMesh == null)
            {
                Transform visualRoot = transform.Find("VisualRoot");
                visualMesh = visualRoot != null ? visualRoot : null;
            }
        }

        public void SetSpeedMultiplier(float mul)
        {
            SpeedMultiplier = Mathf.Clamp(mul, 0.2f, 3f);
        }

        public void SetGripMultiplier(float mul)
        {
            _gripMul = Mathf.Clamp(mul, 0.2f, 3f);
        }

        public void SetBrakeMultiplier(float mul)
        {
            _brakeMul = Mathf.Clamp(mul, 0.2f, 3f);
        }

        private void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector2 moveInput = RuntimeInput.ReadMove();
            _steeringInput = Mathf.Clamp(moveInput.x, -1f, 1f);
            _throttleInput = Mathf.Clamp(moveInput.y, -1f, 1f);

            ApplyThrottle(_throttleInput, dt);
            ApplySteering(_steeringInput, dt);
            ApplyMovement(dt);
            ApplyLean(_steeringInput, dt);
            ApplyDownforce();
        }

        private void ApplyThrottle(float input, float dt)
        {
            float effectiveAccel = Mathf.Max(0f, acceleration * SpeedMultiplier);
            float effectiveMaxSpeed = Mathf.Max(0f, maxMoveSpeed * SpeedMultiplier);
            float effectiveReverseSpeed = Mathf.Max(0f, reverseSpeed);
            float rampDuration = Mathf.Max(0.01f, accelerationSmoothing);
            float reverseThreshold = Mathf.Max(0f, reverseEnableSpeedThreshold);
            float reverseHold = Mathf.Max(0f, reverseHoldSeconds);
            float forwardInput = Mathf.Clamp01(input);
            bool reversePressed = input < -0.01f;

            if (forwardInput > 0f)
            {
                _reverseHoldTimer = 0f;
                _throttleRampValue = Mathf.MoveTowards(_throttleRampValue, forwardInput, dt / rampDuration);
                _targetSpeed = effectiveMaxSpeed * _throttleRampValue;
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, effectiveAccel * dt);
                _currentSpeed = Mathf.Clamp(_currentSpeed, -effectiveReverseSpeed, effectiveMaxSpeed);
                return;
            }

            _throttleRampValue = Mathf.MoveTowards(_throttleRampValue, 0f, dt / rampDuration);

            if (reversePressed)
            {
                _reverseHoldTimer += dt;
                bool movingForwardTooFast = _currentSpeed > reverseThreshold;
                if (movingForwardTooFast)
                {
                    _targetSpeed = 0f;
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, brakeForce * Mathf.Max(0.2f, _brakeMul) * dt);

                    if (_currentSpeed <= reverseThreshold)
                    {
                        _targetSpeed = -effectiveReverseSpeed;
                        float reverseAccel = effectiveAccel * Mathf.Max(0.1f, reverseAccelerationMul);
                        _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, reverseAccel * dt);
                    }
                }
                else
                {
                    bool holdRequired = reverseHold > 0f && _currentSpeed >= 0f;
                    if (holdRequired && _reverseHoldTimer < reverseHold)
                    {
                        _targetSpeed = 0f;
                        _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, brakeForce * Mathf.Max(0.2f, _brakeMul) * dt);
                    }
                    else
                    {
                        _targetSpeed = -effectiveReverseSpeed;
                        float reverseAccel = effectiveAccel * Mathf.Max(0.1f, reverseAccelerationMul);
                        _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, reverseAccel * dt);
                    }
                }
            }
            else
            {
                _reverseHoldTimer = 0f;
                _targetSpeed = 0f;
                float coastDeceleration = Mathf.Max(0f, coastingDeceleration) * Mathf.Max(0.2f, _brakeMul);
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, coastDeceleration * dt);
            }

            _currentSpeed = Mathf.Clamp(_currentSpeed, -effectiveReverseSpeed, effectiveMaxSpeed);
        }

        private void ApplySteering(float input, float dt)
        {
            if (Mathf.Abs(_currentSpeed) < minSpeedToTurn)
            {
                return;
            }

            float effectiveTurnSpeed = turnSpeed * Mathf.Max(0.2f, _gripMul);
            float maxSpeedForRatio = Mathf.Max(0.01f, maxMoveSpeed * SpeedMultiplier);
            float absSpeed = Mathf.Abs(_currentSpeed);
            float speedRatio = absSpeed / maxSpeedForRatio;
            float speedPenalty = 1f - (speedRatio * highSpeedTurnPenalty);
            effectiveTurnSpeed *= Mathf.Clamp(speedPenalty, 0.15f, 1f);

            float lowSpeedRange = Mathf.Max(0.5f, maxSpeedForRatio * 0.35f);
            float lowSpeedRatio = Mathf.Clamp01((absSpeed - minSpeedToTurn) / lowSpeedRange);
            effectiveTurnSpeed *= Mathf.Lerp(lowSpeedSteeringScale, 1f, lowSpeedRatio);

            float turnAmount = input * effectiveTurnSpeed * dt;
            if (_currentSpeed < 0f)
            {
                turnAmount = -turnAmount;
            }

            Quaternion next = rb.rotation * Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(next);
        }

        private void ApplyMovement(float dt)
        {
            Vector3 forwardMovement = transform.forward * _currentSpeed;
            if (enableDrift && Mathf.Abs(_currentSpeed) > driftSpeedThreshold && Mathf.Abs(_steeringInput) > 0.1f)
            {
                float maxSpeedForRatio = Mathf.Max(0.1f, maxMoveSpeed * SpeedMultiplier);
                float driftIntensity = Mathf.Abs(_steeringInput) * driftAmount * Mathf.Max(0.25f, 1f / Mathf.Max(0.3f, _gripMul));
                float speedFactor = _currentSpeed / maxSpeedForRatio;
                Vector3 lateralDrift = -transform.right * _steeringInput * driftIntensity * speedFactor * _currentSpeed;
                forwardMovement += lateralDrift * dt;
            }

            Vector3 nextPosition = rb.position + (forwardMovement * dt);
            rb.MovePosition(nextPosition);
        }

        private void ApplyLean(float steeringInput, float dt)
        {
            if (visualMesh == null || visualMesh == transform)
            {
                return;
            }

            float maxSpeedForRatio = Mathf.Max(0.1f, maxMoveSpeed * SpeedMultiplier);
            float speedRatio = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxSpeedForRatio);
            float targetLean = -steeringInput * maxLeanAngle * speedRatio;
            _currentLeanAngle = Mathf.Lerp(_currentLeanAngle, targetLean, Mathf.Max(0.01f, leanSpeed) * dt);
            visualMesh.localRotation = Quaternion.Euler(0f, 0f, _currentLeanAngle);
        }

        private void ApplyDownforce()
        {
            if (Mathf.Abs(_currentSpeed) <= 0.5f)
            {
                return;
            }

            float maxSpeedForRatio = Mathf.Max(0.1f, maxMoveSpeed * SpeedMultiplier);
            float speedRatio = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxSpeedForRatio);
            float gripScale = Mathf.Max(0.2f, _gripMul);
            Vector3 downforceVector = -transform.up * downforce * speedRatio * gripScale;
            rb.AddForce(downforceVector, ForceMode.Force);
        }
    }
}
