using UnityEngine;

namespace DeliveryRun.Delivery.Vehicle
{
    /// <summary>
    /// Handles position/rotation smoothing, yaw tracking, and speed-based trailing.
    /// </summary>
    internal sealed class CameraFollowController
    {
        private Vector3 _positionVelocity;
        private float _currentYaw;
        private float _yawVelocity;
        private bool _initialized;

        public Vector3 PositionVelocity => _positionVelocity;
        public float CurrentYaw => _currentYaw;

        public void Reset()
        {
            _initialized = false;
            _positionVelocity = Vector3.zero;
            _yawVelocity = 0f;
        }

        public struct FollowParams
        {
            public float HeightOffset;
            public float FollowDistance;
            public float SmoothTime;
            public float PitchAngle;
            public float LookAtBlend;
            public float YawFollowStrength;
            public float YawSmoothTime;
            public float YawDeadZoneDegrees;
            public float ExtraBackAtHighSpeed;
            public float SpeedLagStart;
            public float SpeedLagEnd;
            public float FollowSharpnessLowSpeed;
            public float FollowSharpnessHighSpeed;
            public float OcclusionPivotHeight;
        }

        public struct FollowResult
        {
            public Vector3 DesiredPosition;
            public Quaternion CameraRotation;
            public Vector3 Pivot;
            public float SpeedT;
        }

        /// <summary>
        /// Computes the desired camera position and rotation for this frame.
        /// Does NOT apply the position to the transform; the orchestrator does that.
        /// </summary>
        public FollowResult ComputeDesiredPose(
            Transform target,
            Rigidbody targetRb,
            Transform cameraTransform,
            FollowParams p,
            float deltaTime)
        {
            float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
            float speedT = Mathf.InverseLerp(p.SpeedLagStart, p.SpeedLagEnd, speed);

            float targetYaw = target.eulerAngles.y;
            if (targetRb != null)
            {
                Vector3 flatVelocity = targetRb.linearVelocity;
                flatVelocity.y = 0f;
                if (flatVelocity.sqrMagnitude > 0.25f)
                {
                    float velocityYaw = Mathf.Atan2(flatVelocity.x, flatVelocity.z) * Mathf.Rad2Deg;
                    // Keep mild velocity alignment to reduce jitter, but avoid over-steering away from target heading.
                    float velocityYawBlend = 0.18f * speedT;
                    targetYaw = Mathf.LerpAngle(targetYaw, velocityYaw, velocityYawBlend);
                }
            }

            if (!_initialized)
            {
                _currentYaw = targetYaw;
                _initialized = true;
            }

            float yawDelta = Mathf.DeltaAngle(_currentYaw, targetYaw);
            float yawDeadZone = Mathf.Max(0f, p.YawDeadZoneDegrees);
            float dynamicDeadZone = Mathf.Lerp(yawDeadZone * 1.15f, yawDeadZone * 0.85f, speedT);
            if (Mathf.Abs(yawDelta) < dynamicDeadZone)
            {
                yawDelta = 0f;
            }

            float catchUpT = Mathf.InverseLerp(12f, 65f, Mathf.Abs(yawDelta));
            float followStrength = Mathf.Clamp01((p.YawFollowStrength * 1.28f) + (0.26f * catchUpT));
            float desiredYaw = _currentYaw + (yawDelta * followStrength);
            float baseYawSmoothTime = Mathf.Max(0.01f, p.YawSmoothTime * 0.78f);
            float yawSmoothTime = Mathf.Lerp(baseYawSmoothTime, baseYawSmoothTime * 0.58f, catchUpT);
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, desiredYaw, ref _yawVelocity, yawSmoothTime);

            float followDistanceWithLag = p.FollowDistance + (p.ExtraBackAtHighSpeed * speedT);

            Quaternion yawRotation = Quaternion.Euler(0f, _currentYaw, 0f);
            Vector3 back = yawRotation * Vector3.back;
            Vector3 pivot = target.position + (Vector3.up * Mathf.Max(0.2f, p.OcclusionPivotHeight));
            Vector3 desiredPosition = target.position + (back * followDistanceWithLag) + (Vector3.up * p.HeightOffset);
            Quaternion baseCameraRotation = Quaternion.Euler(p.PitchAngle, _currentYaw, 0f);
            Vector3 lookVector = pivot - desiredPosition;
            Quaternion lookAtRotation = lookVector.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookVector.normalized, Vector3.up)
                : baseCameraRotation;
            Quaternion cameraRotation = Quaternion.Slerp(baseCameraRotation, lookAtRotation, Mathf.Clamp01(p.LookAtBlend));

            return new FollowResult
            {
                DesiredPosition = desiredPosition,
                CameraRotation = cameraRotation,
                Pivot = pivot,
                SpeedT = speedT
            };
        }

        /// <summary>
        /// Applies final smoothing to reach the desired position.
        /// </summary>
        public Vector3 SmoothPosition(
            Vector3 currentPosition,
            Vector3 desiredPosition,
            float smoothTime,
            float sharpness,
            float deltaTime)
        {
            float expT = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * deltaTime);
            if (smoothTime > 0.0001f)
            {
                return Vector3.SmoothDamp(currentPosition, desiredPosition, ref _positionVelocity, smoothTime, Mathf.Infinity, deltaTime);
            }

            return Vector3.Lerp(currentPosition, desiredPosition, expT);
        }
    }
}
