using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class FuelConsumptionRuntime
    {
        private const float MaxFuelDefault = 100f;
        private const float BaseConsumePerSecond = 0.08f;
        private const float SpeedConsumeFactor = 0.02f;
        private const float PublishInterval = 0.1f;

        private readonly FuelService _fuel;
        private readonly EventBus _events;

        private MotorbikeController _bike;
        private bool _depletedSent;
        private float _publishAccum;
        private float _lastPublishedFuel01 = -1f;

        internal FuelConsumptionRuntime(FuelService fuel, EventBus events)
        {
            _fuel = fuel;
            _events = events;
        }

        internal void Initialize()
        {
            _fuel.Reset(MaxFuelDefault);
            _depletedSent = false;
            _publishAccum = 0f;
            PublishFuelState(force: true);
        }

        internal void OnRunStarted()
        {
            _bike = null;
            _fuel.Reset(MaxFuelDefault);
            _depletedSent = false;
            _publishAccum = 0f;
            PublishFuelState(force: true);
        }

        internal void OnRunEnded()
        {
            _bike = null;
            _fuel.RefillToFull();
            _depletedSent = false;
            PublishFuelState(force: true);
        }

        internal void OnShutdown()
        {
            _bike = null;
            PublishFuelState(force: true);
        }

        internal void Tick(float unscaledDeltaTime)
        {
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
                _events.Publish(new FuelDepleted());
            }

            _publishAccum += unscaledDeltaTime;
            if (_publishAccum >= PublishInterval)
            {
                _publishAccum = 0f;
                PublishFuelState(force: false);
            }
        }

        private void PublishFuelState(bool force)
        {
            float fuel01 = _fuel.Fuel01;
            if (!force && Mathf.Abs(fuel01 - _lastPublishedFuel01) < 0.0005f)
            {
                return;
            }

            _lastPublishedFuel01 = fuel01;
            _events.Publish(new FuelStateChanged
            {
                Fuel01 = fuel01,
                CurrentFuel = _fuel.CurrentFuel,
                MaxFuel = _fuel.MaxFuel
            });
        }
    }
}
