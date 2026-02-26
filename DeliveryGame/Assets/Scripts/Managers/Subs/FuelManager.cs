using DeliveryRun;
using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class FuelManager : SubManagerBase
    {
        private const float MaxFuelDefault = 100f;
        private const float BaseConsumePerSecond = 0.08f;
        private const float SpeedConsumeFactor = 0.02f;
        private const float PublishInterval = 0.1f;
        private const float ScenePollInterval = 0.25f;

        private FuelService _fuel;
        private MotorbikeController _bike;
        private bool _runActive;
        private bool _isRunScene;
        private bool _depletedSent;
        private float _publishAccum;
        private float _scenePollAccum;
        private float _lastPublishedFuel01 = -1f;

        public override string Name => nameof(FuelManager);
        public override int InitOrder => 72;

        protected override void OnInitialize()
        {
            _fuel = new FuelService();
            _fuel.Reset(MaxFuelDefault);
            Services.Register(_fuel);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);

            _isRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
            _depletedSent = false;
            PublishFuelState(true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum >= ScenePollInterval)
            {
                _scenePollAccum = 0f;
                _isRunScene = SceneManager.GetActiveScene().name == SceneNames.RunScene;
            }

            if (!_runActive || !_isRunScene)
            {
                return;
            }

            if (_bike == null)
            {
                _bike = Object.FindAnyObjectByType<MotorbikeController>();
            }

            float speed = _bike != null ? Mathf.Max(0f, _bike.CurrentSpeed) : 0f;
            float consume = (BaseConsumePerSecond + (speed * SpeedConsumeFactor)) * unscaledDeltaTime;
            _fuel.Consume(consume);

            if (!_depletedSent && _fuel.IsEmpty)
            {
                _depletedSent = true;
                Events.Publish(new FuelDepleted());
            }

            _publishAccum += unscaledDeltaTime;
            if (_publishAccum >= PublishInterval)
            {
                _publishAccum = 0f;
                PublishFuelState(false);
            }
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            _bike = null;
            PublishFuelState(true);
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            _bike = null;
            _fuel.Reset(MaxFuelDefault);
            _depletedSent = false;
            _publishAccum = 0f;
            PublishFuelState(true);
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
            _bike = null;
            _fuel.RefillToFull();
            _depletedSent = false;
            PublishFuelState(true);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            _isRunScene = evt.SceneName == SceneNames.RunScene;
        }

        private void PublishFuelState(bool force)
        {
            float fuel01 = _fuel.Fuel01;
            if (!force && Mathf.Abs(fuel01 - _lastPublishedFuel01) < 0.0005f)
            {
                return;
            }

            _lastPublishedFuel01 = fuel01;
            Events.Publish(new FuelStateChanged
            {
                Fuel01 = fuel01,
                CurrentFuel = _fuel.CurrentFuel,
                MaxFuel = _fuel.MaxFuel
            });
        }
    }
}
