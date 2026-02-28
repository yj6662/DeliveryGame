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
            float targetYaw = target.eulerAngles.y;
            if (!_initialized)
            {
                _currentYaw = targetYaw;
                _initialized = true;
            }

            float yawDelta = Mathf.DeltaAngle(_currentYaw, targetYaw);
            if (Mathf.Abs(yawDelta) < Mathf.Max(0f, p.YawDeadZoneDegrees))
            {
                yawDelta = 0f;
            }

            float desiredYaw = _currentYaw + (yawDelta * Mathf.Clamp01(p.YawFollowStrength));
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, desiredYaw, ref _yawVelocity, Mathf.Max(0.01f, p.YawSmoothTime));

            float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
            float speedT = Mathf.InverseLerp(p.SpeedLagStart, p.SpeedLagEnd, speed);
            float followDistanceWithLag = p.FollowDistance + (p.ExtraBackAtHighSpeed * speedT);

            Quaternion yawRotation = Quaternion.Euler(0f, _currentYaw, 0f);
            Vector3 back = yawRotation * Vector3.back;
            Quaternion cameraRotation = Quaternion.Euler(p.PitchAngle, _currentYaw, 0f);
            Vector3 pivot = target.position + (Vector3.up * Mathf.Max(0.2f, p.OcclusionPivotHeight));
            Vector3 desiredPosition = target.position + (back * followDistanceWithLag) + (Vector3.up * p.HeightOffset);

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
