using System;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudOfferPreviewState
    {
        internal string CurrentOfferId { get; private set; } = "A1";
        internal string CurrentPickupName { get; private set; } = string.Empty;
        internal string CurrentDeliveryName { get; private set; } = string.Empty;
        internal int CurrentOfferReward { get; private set; }
        internal float CurrentOfferRemaining { get; private set; }
        internal float CurrentOfferDuration { get; private set; }
        internal bool OfferAcceptWindow { get; private set; }
        internal bool PreviewVisible { get; private set; }

        private double _offerUiExpireAt;
        private bool _offerUiPauseActive;
        private double _offerUiPauseStartedAt;
        private int _offerUiLastTenth = -1;

        internal void Reset()
        {
            CurrentOfferId = "A1";
            CurrentPickupName = string.Empty;
            CurrentDeliveryName = string.Empty;
            CurrentOfferReward = 0;
            CurrentOfferRemaining = 0f;
            CurrentOfferDuration = 0f;
            OfferAcceptWindow = false;
            PreviewVisible = false;
            _offerUiExpireAt = 0d;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = -1;
        }

        internal void Spawned(string offerId, string pickupName, string deliveryName, int reward, float ttlSeconds, double now)
        {
            CurrentOfferId = string.IsNullOrEmpty(offerId) ? "A1" : offerId;
            CurrentPickupName = pickupName;
            CurrentDeliveryName = deliveryName;
            CurrentOfferReward = reward;
            CurrentOfferRemaining = ttlSeconds;
            CurrentOfferDuration = Mathf.Max(0.01f, ttlSeconds);
            OfferAcceptWindow = true;
            PreviewVisible = true;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = Mathf.FloorToInt(CurrentOfferRemaining * 10f);
            _offerUiExpireAt = now + ttlSeconds;
        }

        internal bool TryApplyOfferTick(string offerId, float remainingSeconds)
        {
            if (!string.Equals(offerId, CurrentOfferId, StringComparison.Ordinal))
            {
                return false;
            }

            CurrentOfferRemaining = remainingSeconds;
            _offerUiLastTenth = Mathf.FloorToInt(CurrentOfferRemaining * 10f);
            return PreviewVisible;
        }

        internal bool IsCurrentOffer(string offerId)
        {
            return string.Equals(offerId, CurrentOfferId, StringComparison.Ordinal);
        }

        internal void CloseWindow(bool clearRemaining)
        {
            OfferAcceptWindow = false;
            PreviewVisible = false;
            if (clearRemaining)
            {
                CurrentOfferRemaining = 0f;
            }

            _offerUiExpireAt = 0d;
            _offerUiPauseActive = false;
            _offerUiPauseStartedAt = 0d;
            _offerUiLastTenth = -1;
        }

        internal void OnMusicModalStateChanged(bool isOpen, double now)
        {
            if (!OfferAcceptWindow || !PreviewVisible)
            {
                _offerUiPauseActive = false;
                _offerUiPauseStartedAt = 0d;
                return;
            }

            if (isOpen)
            {
                if (_offerUiPauseActive)
                {
                    return;
                }

                _offerUiPauseActive = true;
                _offerUiPauseStartedAt = now;
                return;
            }

            if (!_offerUiPauseActive)
            {
                return;
            }

            _offerUiPauseActive = false;
            double pausedSeconds = now - _offerUiPauseStartedAt;
            _offerUiPauseStartedAt = 0d;
            if (pausedSeconds > 0d && _offerUiExpireAt > 0d)
            {
                _offerUiExpireAt += pausedSeconds;
            }
        }

        internal bool TickCountdown(double now)
        {
            if (!OfferAcceptWindow || !PreviewVisible || _offerUiPauseActive || _offerUiExpireAt <= 0d)
            {
                return false;
            }

            double remainingSeconds = _offerUiExpireAt - now;
            if (remainingSeconds < 0d)
            {
                remainingSeconds = 0d;
            }

            float remainingFloat = (float)remainingSeconds;
            int tenth = Mathf.FloorToInt(remainingFloat * 10f);
            bool changed = tenth != _offerUiLastTenth;
            _offerUiLastTenth = tenth;
            CurrentOfferRemaining = remainingFloat;
            return changed;
        }
    }
}
