using DeliveryRun.Delivery.Orders;
using DeliveryRun.Managers.Core;
using DeliveryRun.Music;
using UnityEngine;

namespace DeliveryRun.UI.Features
{
    internal sealed partial class RunHudUiFeature
    {
        private void OnMusicChoiceSelected(MusicChoiceSelected evt)
        {
            string trackName = ResolveTrackName(evt);
            if (evt.ChoiceIndex >= 0 && evt.ChoiceIndex < ChoiceSlots)
            {
                _pickedGenreByChoice[evt.ChoiceIndex] = evt.GenreId;
                _pickedTrackNameByChoice[evt.ChoiceIndex] = trackName;
                RebuildSynergyLine();
            }

            RefreshTrackPlayerText();
            ApplyStatusLines();
        }

        private void OnMusicChoiceModalStateChanged(MusicChoiceModalStateChanged evt)
        {
            _musicChoiceModalOpen = evt.IsOpen;
            _phoneCoordinator.OnMusicChoiceModalStateChanged(evt.IsOpen, Clock.Now);
        }

        private void OnSpeedMultiplierChanged(PlayerMoveSpeedMultiplierChanged evt)
        {
            _speedMultiplier = evt.Multiplier;
            RebuildBuffLineFromModifiers();
            ApplyStatusLines();
        }

        private void OnModifiersChanged(RunModifiersChanged evt)
        {
            RebuildBuffLineFromModifiers();
            ApplyStatusLines();
        }

        private void OnModifiersCleared(RunModifiersCleared evt)
        {
            _speedMultiplier = 1f;
            RebuildBuffLineFromModifiers();
            ClearMusicChoiceSelection();
            RebuildSynergyLine();
            RefreshTrackPlayerText();
            ApplyStatusLines();
        }

        private void OnOfferSpawned(OfferSpawned evt)
        {
            _phoneCoordinator.OnOfferSpawned(evt, Clock.Now);
        }

        private void OnOfferTicked(OfferTicked evt)
        {
            _phoneCoordinator.OnOfferTicked(evt);
        }

        private void OnOfferExpired(OfferExpired evt)
        {
            _phoneCoordinator.OnOfferExpired(evt);
        }

        private void OnOfferAccepted(OfferAccepted evt)
        {
            _phoneCoordinator.OnOfferAccepted(evt);
        }

        private void OnOrderPickupReached(OrderPickupReached evt)
        {
            _phoneCoordinator.OnOrderPickupReached(evt);
        }

        private void OnOrderCompleted(OrderCompleted evt)
        {
            _phoneCoordinator.OnOrderCompleted(evt);
            ApplyCashLabel();

            if (evt.Reward > 0)
            {
                EnsurePlayerReference();
                if (_player != null)
                {
                    float quality01 = 1f;
                    if (!string.IsNullOrEmpty(evt.OfferId))
                    {
                        if (_completedOrderQualityByOffer.TryGetValue(evt.OfferId, out float cachedQuality))
                        {
                            quality01 = Mathf.Clamp01(cachedQuality);
                        }

                        _completedOrderQualityByOffer.Remove(evt.OfferId);
                    }

                    _worldOverlayCoordinator.ShowOrderRewardPopup(evt.Reward, quality01, _player.transform.position);
                }
            }
        }

        private void OnOrderTimedOut(OrderTimedOut evt)
        {
            _phoneCoordinator.OnOrderTimedOut(evt);
            if (!string.IsNullOrEmpty(evt.OfferId))
            {
                _completedOrderQualityByOffer.Remove(evt.OfferId);
            }
        }

        private void OnOrderObjectiveUpdated(OrderObjectiveUpdated evt)
        {
            _phoneCoordinator.OnOrderObjectiveUpdated(evt);
        }

        private void OnOrderObjectiveMarkerUpdated(OrderObjectiveMarkerUpdated evt)
        {
            _worldOverlayCoordinator.SetObjectiveMarker(evt.Active, evt.WorldPosition, evt.PointType);
        }

        private void OnOrderObjectiveMarkersUpdated(OrderObjectiveMarkersUpdated evt)
        {
            _worldOverlayCoordinator.SetObjectiveMarkers(evt);
        }

        private void OnSessionBalanceChanged(SessionBalanceChanged evt)
        {
            _sessionBalance = evt.Balance;
            ApplyCashLabel();
        }

        private void OnFoodStateTicked(FoodStateTicked evt)
        {
            _phoneCoordinator.OnFoodStateTicked(evt);
        }

        private void OnFoodQualityComputed(FoodQualityComputed evt)
        {
            if (string.IsNullOrEmpty(evt.OfferId))
            {
                return;
            }

            _completedOrderQualityByOffer[evt.OfferId] = Mathf.Clamp01(evt.Quality01);
        }

        private void OnFuelStateChanged(FuelStateChanged evt)
        {
            _fuel01 = Mathf.Clamp01(evt.Fuel01);
            _corePanelsCoordinator.SetFuel(_fuel01);
        }

        private void OnFuelRefuelStateChanged(FuelRefuelStateChanged evt)
        {
            _statusDisplay?.SetRefuelState(evt.IsRefueling, evt.FuelPerSecond, evt.CostPerSecond);
            ApplyStatusLines();
        }
    }
}
