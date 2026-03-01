using System;
using System.Collections.Generic;
using DeliveryRun.Managers.Core;
using UnityEngine;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudPhoneState
    {
        private readonly HudActiveOrderStore _activeOrders;
        private readonly HudOrderCache _orderCache;
        private readonly HudOfferPreviewState _offerPreview;

        private float _foodTemperature01;
        private float _foodSpill01;
        private float _foodQuality01;
        private bool _hasFoodState;
        private string _foodOfferId;
        private int _sessionBonus;

        internal HudPhoneState(int maxTrackedOrders)
        {
            _activeOrders = new HudActiveOrderStore(maxTrackedOrders);
            _orderCache = new HudOrderCache(maxTrackedOrders);
            _offerPreview = new HudOfferPreviewState();
        }

        internal HudActiveOrderStore ActiveOrders => _activeOrders;
        internal Dictionary<string, bool> CarryingByOffer => _orderCache.CarryingByOffer;
        internal int SessionBonus => _sessionBonus;

        internal void ResetForSceneExit()
        {
            _offerPreview.Reset();
            _foodOfferId = null;
            _hasFoodState = false;
            _foodTemperature01 = 0f;
            _foodSpill01 = 0f;
            _foodQuality01 = 0f;
            _orderCache.Clear();
        }

        internal void ResetForRunSession()
        {
            _sessionBonus = 0;
            _hasFoodState = false;
            _foodOfferId = null;
            _foodTemperature01 = 0f;
            _foodSpill01 = 0f;
            _foodQuality01 = 0f;
            _offerPreview.Reset();
            _activeOrders.Clear();
            _orderCache.Clear();
        }

        internal void ResetAll()
        {
            ResetForRunSession();
        }

        internal void OnMusicChoiceModalStateChanged(bool isOpen, double now)
        {
            _offerPreview.OnMusicModalStateChanged(isOpen, now);
        }

        internal void OnOfferSpawned(OfferSpawned evt, double now)
        {
            _offerPreview.Spawned(evt.OfferId, evt.PickupName, evt.DeliveryName, evt.Reward, evt.TtlSeconds, now);
            _orderCache.RecordOffer(
                _offerPreview.CurrentOfferId,
                _offerPreview.CurrentPickupName,
                _offerPreview.CurrentDeliveryName,
                evt.Reward);
        }

        internal bool OnOfferTicked(OfferTicked evt)
        {
            return _offerPreview.TryApplyOfferTick(evt.OfferId, evt.RemainingSeconds);
        }

        internal bool OnOfferExpired(OfferExpired evt)
        {
            if (!_offerPreview.IsCurrentOffer(evt.OfferId))
            {
                return false;
            }

            _offerPreview.CloseWindow(true);
            _activeOrders.Remove(evt.OfferId);
            _orderCache.RemoveOffer(evt.OfferId);
            return true;
        }

        internal void OnOfferAccepted(OfferAccepted evt)
        {
            if (_offerPreview.IsCurrentOffer(evt.OfferId))
            {
                _offerPreview.CloseWindow(false);
            }

            string pickupName = _orderCache.ResolvePickupName(evt.OfferId, _offerPreview.CurrentPickupName);
            _activeOrders.Upsert(evt.OfferId, string.IsNullOrEmpty(pickupName) ? "GO PICKUP" : "GO PICKUP: " + pickupName);
            _orderCache.SetCarrying(evt.OfferId, false);
        }

        internal void OnOrderPickupReached(OrderPickupReached evt)
        {
            string deliveryName = _orderCache.ResolveDeliveryName(evt.OfferId, _offerPreview.CurrentDeliveryName);
            _foodOfferId = evt.OfferId;
            _orderCache.SetCarrying(evt.OfferId, true);
            _activeOrders.Upsert(evt.OfferId, string.IsNullOrEmpty(deliveryName) ? "DELIVER TO" : "DELIVER TO: " + deliveryName);
        }

        internal void OnOrderCompleted(OrderCompleted evt)
        {
            if (_offerPreview.IsCurrentOffer(evt.OfferId))
            {
                _offerPreview.CloseWindow(false);
            }

            int fallbackReward = _offerPreview.CurrentOfferReward > 0 ? _offerPreview.CurrentOfferReward : evt.Reward;
            int baseReward = _orderCache.ResolveBaseReward(evt.OfferId, fallbackReward);
            _sessionBonus += evt.Reward - baseReward;

            _activeOrders.Remove(evt.OfferId);
            _orderCache.RemoveOffer(evt.OfferId);
            ClearFoodStateIfMatchedOffer(evt.OfferId);
        }

        internal void OnOrderTimedOut(OrderTimedOut evt)
        {
            if (_offerPreview.IsCurrentOffer(evt.OfferId))
            {
                _offerPreview.CloseWindow(false);
            }

            _activeOrders.Remove(evt.OfferId);
            _orderCache.RemoveOffer(evt.OfferId);
            ClearFoodStateIfMatchedOffer(evt.OfferId);
        }

        internal bool OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            if (string.IsNullOrEmpty(evt.Text) || _activeOrders.Count <= 0)
            {
                return false;
            }

            string targetOfferId = evt.OfferId;
            if (string.IsNullOrEmpty(targetOfferId))
            {
                targetOfferId = _activeOrders.PrimaryOfferId;
            }

            if (string.IsNullOrEmpty(targetOfferId))
            {
                return false;
            }

            _activeOrders.Upsert(targetOfferId, evt.Text);
            return true;
        }

        internal void OnFoodStateTicked(FoodStateTicked evt)
        {
            _hasFoodState = true;
            _foodTemperature01 = Mathf.Clamp01(evt.Temperature01);
            _foodSpill01 = Mathf.Clamp01(evt.Spill01);
            _foodQuality01 = Mathf.Clamp01(evt.Quality01);
        }

        internal bool TryConsumeOfferAccept(bool musicChoiceModalOpen, out string offerId)
        {
            offerId = null;
            if (musicChoiceModalOpen || !_offerPreview.OfferAcceptWindow)
            {
                return false;
            }

            offerId = _offerPreview.CurrentOfferId;
            _offerPreview.CloseWindow(false);
            return true;
        }

        internal bool TickOfferCountdown(double now)
        {
            return _offerPreview.TickCountdown(now);
        }

        internal HudPhonePanel.PhoneViewState BuildViewState()
        {
            return new HudPhonePanel.PhoneViewState
            {
                ActiveOrderCount = _activeOrders.Count,
                HasFoodState = _hasFoodState,
                FoodTemperature01 = _foodTemperature01,
                FoodSpill01 = _foodSpill01,
                FoodOfferId = _foodOfferId,
                PreviewVisible = _offerPreview.PreviewVisible,
                OfferAcceptWindow = _offerPreview.OfferAcceptWindow,
                CurrentOfferDuration = _offerPreview.CurrentOfferDuration,
                CurrentOfferRemaining = _offerPreview.CurrentOfferRemaining,
                CurrentPickupName = _offerPreview.CurrentPickupName,
                CurrentDeliveryName = _offerPreview.CurrentDeliveryName,
                CurrentOfferReward = _offerPreview.CurrentOfferReward
            };
        }

        private void ClearFoodStateIfMatchedOffer(string offerId)
        {
            if (!string.Equals(_foodOfferId, offerId, StringComparison.Ordinal))
            {
                return;
            }

            _foodOfferId = null;
            _hasFoodState = false;
            _foodTemperature01 = 0f;
            _foodSpill01 = 0f;
            _foodQuality01 = 0f;
        }
    }
}
