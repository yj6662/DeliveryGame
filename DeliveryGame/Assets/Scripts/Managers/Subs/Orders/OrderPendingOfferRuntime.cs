using System;

namespace DeliveryRun.Managers.Subs
{
    internal enum PendingOfferTickOutcome
    {
        None = 0,
        Ticked = 1,
        Expired = 2
    }

    internal sealed class OrderPendingOfferRuntime
    {
        private OrderPendingOffer _pendingOffer;
        private bool _offerPauseActive;
        private double _offerPauseStartedAt;
        private float _offerTickAccum;

        internal bool HasPendingOffer => _pendingOffer.Active;
        internal OrderPendingOffer Current => _pendingOffer;

        internal void Set(OrderPendingOffer pendingOffer)
        {
            _pendingOffer = pendingOffer;
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;
            _offerTickAccum = 0f;
        }

        internal void Clear()
        {
            _pendingOffer = default;
            _offerPauseActive = false;
            _offerPauseStartedAt = 0d;
            _offerTickAccum = 0f;
        }

        internal void OnMusicChoiceModalStateChanged(bool isOpen, double now)
        {
            if (!_pendingOffer.Active)
            {
                _offerPauseActive = false;
                _offerPauseStartedAt = 0d;
                return;
            }

            if (isOpen)
            {
                if (_offerPauseActive)
                {
                    return;
                }

                _offerPauseActive = true;
                _offerPauseStartedAt = now;
                return;
            }

            if (!_offerPauseActive)
            {
                return;
            }

            _offerPauseActive = false;
            double paused = now - _offerPauseStartedAt;
            if (paused > 0d)
            {
                _pendingOffer.EndTime += paused;
            }

            _offerPauseStartedAt = 0d;
        }

        internal PendingOfferTickOutcome Tick(float unscaledDeltaTime, float tickInterval, double now, out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (!_pendingOffer.Active || _offerPauseActive)
            {
                return PendingOfferTickOutcome.None;
            }

            float remaining = (float)(_pendingOffer.EndTime - now);
            if (remaining <= 0f)
            {
                remainingSeconds = 0f;
                return PendingOfferTickOutcome.Expired;
            }

            _offerTickAccum += unscaledDeltaTime;
            if (_offerTickAccum < tickInterval)
            {
                return PendingOfferTickOutcome.None;
            }

            _offerTickAccum = 0f;
            remainingSeconds = remaining;
            return PendingOfferTickOutcome.Ticked;
        }
    }
}
