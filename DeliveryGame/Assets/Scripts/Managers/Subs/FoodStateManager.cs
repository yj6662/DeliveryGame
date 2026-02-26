using DeliveryRun.Delivery.Vehicle;
using DeliveryRun.Managers.Core;
using UnityEngine;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class FoodStateManager : SubManagerBase
    {
        private const float UiTickInterval = 0.25f;

        private FoodStateService _food;
        private FoodStateConfigSO _cfg;
        private MotorbikeController _bike;
        private Rigidbody _rb;
        private BikeCollisionReporter _collisionReporter;
        private float _prevSpeed;
        private float _collisionImpulseAccum;
        private float _uiTickAccum;

        public override string Name => nameof(FoodStateManager);
        public override int InitOrder => 72;

        protected override void OnInitialize()
        {
            _cfg = Resources.Load<FoodStateConfigSO>("Bootstrap/FoodStateConfig");
            if (_cfg == null)
            {
                Debug.LogError("[FoodStateManager] Missing config: Resources/Bootstrap/FoodStateConfig");
            }

            _food = new FoodStateService();
            Services.Register(_food);

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<OrderPickupReached>(Events, OnPickupReached);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            if (_food == null || !_food.IsActive || _cfg == null)
            {
                return;
            }

            CacheBikeIfNeeded();
            if (_rb == null)
            {
                return;
            }

            float speed = _rb.linearVelocity.magnitude;
            float decel = 0f;
            if (unscaledDeltaTime > 0.00001f)
            {
                decel = Mathf.Max(0f, (_prevSpeed - speed) / unscaledDeltaTime);
            }

            _prevSpeed = speed;
            float yawRateAbs = Mathf.Abs(Vector3.Dot(_rb.angularVelocity, Vector3.up));
            float impulse = _collisionImpulseAccum;
            _collisionImpulseAccum = 0f;

            _food.TickUnscaled(unscaledDeltaTime, speed, yawRateAbs, decel, impulse, _cfg);

            _uiTickAccum += unscaledDeltaTime;
            if (_uiTickAccum >= UiTickInterval)
            {
                _uiTickAccum = 0f;
                float quality = _food.ComputeQuality01();
                Events.Publish(new FoodStateTicked
                {
                    Temperature01 = _food.Temperature01,
                    Spill01 = _food.Spill01,
                    Quality01 = quality
                });
            }
        }

        protected override void OnShutdown()
        {
            if (_food != null)
            {
                _food.Reset();
            }

            UnhookCollisionReporter();
            _bike = null;
            _rb = null;
            _uiTickAccum = 0f;
            _collisionImpulseAccum = 0f;
            _prevSpeed = 0f;
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            if (_food != null)
            {
                _food.Reset();
            }

            _uiTickAccum = 0f;
            _collisionImpulseAccum = 0f;
            CacheBikeIfNeeded();
            _prevSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            if (_food != null)
            {
                _food.Reset();
            }

            UnhookCollisionReporter();
            _bike = null;
            _rb = null;
            _uiTickAccum = 0f;
            _collisionImpulseAccum = 0f;
            _prevSpeed = 0f;
        }

        private void OnPickupReached(OrderPickupReached evt)
        {
            CacheBikeIfNeeded();
            if (_cfg == null || _food == null)
            {
                return;
            }

            _food.StartForOffer(evt.OfferId, _cfg);
            _uiTickAccum = 0f;
            _collisionImpulseAccum = 0f;
            _prevSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        }

        private void CacheBikeIfNeeded()
        {
            if (_bike == null)
            {
                _bike = Object.FindAnyObjectByType<MotorbikeController>();
            }

            if (_bike == null)
            {
                return;
            }

            if (_rb == null)
            {
                _rb = _bike.GetComponentInParent<Rigidbody>();
                if (_rb == null)
                {
                    _rb = _bike.GetComponent<Rigidbody>();
                }
            }

            if (_collisionReporter == null)
            {
                _collisionReporter = _bike.GetComponent<BikeCollisionReporter>();
                if (_collisionReporter == null)
                {
                    _collisionReporter = _bike.gameObject.AddComponent<BikeCollisionReporter>();
                }

                _collisionReporter.Collided -= OnBikeCollided;
                _collisionReporter.Collided += OnBikeCollided;
            }
        }

        private void UnhookCollisionReporter()
        {
            if (_collisionReporter == null)
            {
                return;
            }

            _collisionReporter.Collided -= OnBikeCollided;
            _collisionReporter = null;
        }

        private void OnBikeCollided(float impulse)
        {
            _collisionImpulseAccum += impulse;
        }
    }
}
