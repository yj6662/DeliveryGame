using DeliveryRun.Managers.Core;
using UnityEngine;
using DomainRunSessionEnded = DeliveryRun.Delivery.RunSession.RunSessionEnded;
using DomainRunSessionStarted = DeliveryRun.Delivery.RunSession.RunSessionStarted;

namespace DeliveryRun.Managers.Subs
{
    public sealed class RatingManager : SubManagerBase
    {
        private RatingConfigSO _cfg;
        private RatingService _service;
        private bool _runActive;
        private bool _zeroSent;

        public override string Name => nameof(RatingManager);
        public override int InitOrder => 74;

        public float CurrentRating => _service != null ? _service.Rating : 0f;
        public bool IsDepleted => CurrentRating <= 0.0001f;

        protected override void OnInitialize()
        {
            _cfg = Resources.Load<RatingConfigSO>("Bootstrap/RatingConfig");
            if (_cfg == null)
            {
                Debug.LogError("[RatingManager] Missing config: Resources/Bootstrap/RatingConfig");
            }

            _service = new RatingService();
            Services.Register(_service);

            _runActive = false;
            _zeroSent = false;

            Subs.Add<DomainRunSessionStarted>(Events, OnRunStarted);
            Subs.Add<DomainRunSessionEnded>(Events, OnRunEnded);
            Subs.Add<FoodQualityComputed>(Events, OnFoodQualityComputed);
        }

        protected override void OnShutdown()
        {
            _runActive = false;
            _zeroSent = false;
            if (_service != null)
            {
                _service.Reset(0f);
            }
        }

        private void OnRunStarted(DomainRunSessionStarted evt)
        {
            _runActive = true;
            _zeroSent = false;

            float start = _cfg != null ? _cfg.StartRating : 5f;
            _service.Reset(start);

            Events.Publish(new RatingChanged
            {
                Rating = _service.Rating,
                Delta = 0f,
                Reason = "run_start"
            });
        }

        private void OnRunEnded(DomainRunSessionEnded evt)
        {
            _runActive = false;
        }

        private void OnFoodQualityComputed(FoodQualityComputed evt)
        {
            if (!_runActive || _service == null)
            {
                return;
            }

            if (_cfg == null)
            {
                return;
            }

            float quality = evt.Quality01;
            float delta;
            if (quality >= _cfg.Q_Excellent)
            {
                delta = _cfg.Delta_Excellent;
            }
            else if (quality >= _cfg.Q_Good)
            {
                delta = _cfg.Delta_Good;
            }
            else if (quality >= _cfg.Q_Ok)
            {
                delta = _cfg.Delta_Ok;
            }
            else if (quality >= _cfg.Q_Bad)
            {
                delta = _cfg.Delta_Bad;
            }
            else
            {
                delta = _cfg.Delta_Terrible;
            }

            ApplyDelta(delta);
        }

        private void ApplyDelta(float delta)
        {
            float before = _service.Rating;
            float after = _service.ApplyDelta(delta);
            float appliedDelta = after - before;
            Events.Publish(new RatingChanged
            {
                Rating = after,
                Delta = appliedDelta,
                Reason = "delivery_quality"
            });

            if (!_zeroSent && after <= 0.0001f)
            {
                _zeroSent = true;
                Events.Publish(new RatingZeroReached { Rating = after });
                // Legacy event for older listeners (SFX/telemetry/UI).
                Events.Publish(new RatingDepleted());
            }
        }
    }
}
