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
        private readonly Dictionary<string, FoodStateTicked> _foodStateByOffer = new Dictionary<string, FoodStateTicked>(8);
        private int _sessionBonus;

        internal HudPhoneState(int maxTrackedOrders)
        {
            _activeOrders = new HudActiveOrderStore(maxTrackedOrders);
            _orderCache = new HudOrderCache(maxTrackedOrders);
            _offerPreview = new HudOfferPreviewState();
        }

        internal HudActiveOrderStore ActiveOrders => _activeOrders;
        internal Dictionary<string, bool> CarryingByOffer => _orderCache.CarryingByOffer;
        internal Dictionary<string, FoodStateTicked> FoodStateByOffer => _foodStateByOffer;
        internal int SessionBonus => _sessionBonus;

        internal void ResetForSceneExit()
        {
            _offerPreview.Reset();
            _foodStateByOffer.Clear();
            _orderCache.Clear();
        }

        internal void ResetForRunSession()
        {
            _sessionBonus = 0;
            _foodStateByOffer.Clear();
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
                evt.FoodName,
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

            _orderCache.SetCarrying(evt.OfferId, false);
            string foodName = _orderCache.ResolveFoodName(evt.OfferId, string.Empty);
            _activeOrders.Upsert(
                evt.OfferId,
                BuildActiveOrderStatusText(false, foodName));
        }

        internal void OnOrderPickupReached(OrderPickupReached evt)
        {
            if (!string.IsNullOrEmpty(evt.OfferId))
            {
                _foodStateByOffer[evt.OfferId] = new FoodStateTicked
                {
                    OfferId = evt.OfferId,
                    Temperature01 = 1f,
                    Spill01 = 0f,
                    Quality01 = 1f
                };
            }

            _orderCache.SetCarrying(evt.OfferId, true);
            string foodName = _orderCache.ResolveFoodName(evt.OfferId, evt.FoodName);
            _activeOrders.Upsert(
                evt.OfferId,
                BuildActiveOrderStatusText(true, foodName));
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
            RemoveFoodState(evt.OfferId);
        }

        internal void OnOrderTimedOut(OrderTimedOut evt)
        {
            if (_offerPreview.IsCurrentOffer(evt.OfferId))
            {
                _offerPreview.CloseWindow(false);
            }

            _activeOrders.Remove(evt.OfferId);
            _orderCache.RemoveOffer(evt.OfferId);
            RemoveFoodState(evt.OfferId);
        }

        internal bool OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            if (_activeOrders.Count <= 0)
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

            // Ignore stale objective updates from already-completed/removed offers.
            if (!_activeOrders.Contains(targetOfferId))
            {
                return false;
            }

            string foodName = _orderCache.ResolveFoodName(targetOfferId, string.Empty);
            _activeOrders.Upsert(targetOfferId, BuildActiveOrderStatusText(IsOfferCarrying(targetOfferId), foodName));
            return true;
        }

        internal void OnFoodStateTicked(FoodStateTicked evt)
        {
            if (string.IsNullOrEmpty(evt.OfferId))
            {
                return;
            }

            _foodStateByOffer[evt.OfferId] = new FoodStateTicked
            {
                OfferId = evt.OfferId,
                Temperature01 = Mathf.Clamp01(evt.Temperature01),
                Spill01 = Mathf.Clamp01(evt.Spill01),
                Quality01 = Mathf.Clamp01(evt.Quality01)
            };
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
                PreviewVisible = _offerPreview.PreviewVisible,
                OfferAcceptWindow = _offerPreview.OfferAcceptWindow,
                CurrentOfferDuration = _offerPreview.CurrentOfferDuration,
                CurrentOfferRemaining = _offerPreview.CurrentOfferRemaining,
                CurrentPickupName = _offerPreview.CurrentPickupName,
                CurrentDeliveryName = _offerPreview.CurrentDeliveryName,
                CurrentOfferReward = _offerPreview.CurrentOfferReward
            };
        }

        private void RemoveFoodState(string offerId)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            _foodStateByOffer.Remove(offerId);
        }

        private bool IsOfferCarrying(string offerId)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return false;
            }

            if (_orderCache.CarryingByOffer.TryGetValue(offerId, out bool isCarrying))
            {
                return isCarrying;
            }

            return false;
        }

        private static string BuildActiveOrderStatusText(bool isCarrying, string foodName)
        {
            string status = isCarrying ? "Deliver" : "PickUp";
            if (string.IsNullOrEmpty(foodName))
            {
                return status;
            }

            return status + " · " + foodName;
        }
    }
}
