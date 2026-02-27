using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;

namespace DeliveryRun.Managers.Subs
{
    public sealed class PlayerBikeModifierLink : SubManagerBase
    {
        private const float PollInterval = 0.1f;

        private MotorbikeController _bike;
        private ModifierStackService _stack;

        private float _speedMul = 1f;
        private float _turnMul = 1f;
        private float _accelMul = 1f;
        private float _gripMul = 1f;
        private float _brakeMul = 1f;
        private float _pollAccum;

        public override string Name => nameof(PlayerBikeModifierLink);
        public override int InitOrder => 50;

        protected override void OnInitialize()
        {
            Services.TryGet(out _stack);

            Subs.Add<RunModifiersChanged>(Events, OnModifiersChanged);
            Subs.Add<RunModifiersCleared>(Events, OnModifiersCleared);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunSessionEnded);

            if (SceneManager.GetActiveScene().name == SceneNames.RunScene)
            {
                RefreshFromStack();
                FindBikeOnce();
                ApplyToBike();
            }
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (SceneManager.GetActiveScene().name != SceneNames.RunScene)
            {
                return;
            }

            _pollAccum += unscaledDeltaTime;
            if (_pollAccum < PollInterval)
            {
                return;
            }

            _pollAccum = 0f;
            if (_bike == null)
            {
                FindBikeOnce();
            }

            ApplyToBike();
        }

        protected override void OnShutdown()
        {
            ResetBike();
            _bike = null;
        }

        private void OnModifiersChanged(RunModifiersChanged evt)
        {
            RefreshFromStack();
            FindBikeOnce();
            ApplyToBike();
        }

        private void OnModifiersCleared(RunModifiersCleared evt)
        {
            _speedMul = 1f;
            _turnMul = 1f;
            _accelMul = 1f;
            _gripMul = 1f;
            _brakeMul = 1f;
            ApplyToBike();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            if (evt.SceneName == SceneNames.RunScene)
            {
                _bike = null;
                RefreshFromStack();
                FindBikeOnce();
                ApplyToBike();
                return;
            }

            ResetBike();
            _bike = null;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            ResetBike();
            _bike = null;
        }

        private void OnRunSessionEnded(DomainRunSessionEnded evt)
        {
            ResetBike();
        }

        private void FindBikeOnce()
        {
            if (_bike != null)
            {
                return;
            }

            _bike = Object.FindAnyObjectByType<MotorbikeController>();
        }

        private void RefreshFromStack()
        {
            if (_stack == null)
            {
                Services.TryGet(out _stack);
            }

            if (_stack == null)
            {
                _speedMul = 1f;
                _gripMul = 1f;
                _brakeMul = 1f;
                return;
            }

            _speedMul = _stack.GetMul(RunStatId.PlayerMoveSpeedMultiplier);
            _turnMul = _stack.GetMul(RunStatId.BikeTurnSensitivityMultiplier);
            _accelMul = _stack.GetMul(RunStatId.BikeAccelerationMultiplier);
            _gripMul = _stack.GetMul(RunStatId.BikeLateralGripMultiplier);
            _brakeMul = _stack.GetMul(RunStatId.BikeBrakeForceMultiplier);
        }

        private void ApplyToBike()
        {
            if (_bike == null)
            {
                return;
            }

            _bike.SetSpeedMultiplier(_speedMul);
            _bike.SetTurnSensitivityMultiplier(_turnMul);
            _bike.SetAccelerationMultiplier(_accelMul);
            _bike.SetGripMultiplier(_gripMul);
            _bike.SetBrakeMultiplier(_brakeMul);
        }

        private void ResetBike()
        {
            _speedMul = 1f;
            _turnMul = 1f;
            _accelMul = 1f;
            _gripMul = 1f;
            _brakeMul = 1f;

            if (_bike == null)
            {
                return;
            }

            _bike.SetSpeedMultiplier(1f);
            _bike.SetTurnSensitivityMultiplier(1f);
            _bike.SetAccelerationMultiplier(1f);
            _bike.SetGripMultiplier(1f);
            _bike.SetBrakeMultiplier(1f);
        }
    }
}
